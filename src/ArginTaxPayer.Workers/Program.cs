using ArginTaxPayer.Application;
using ArginTaxPayer.Infrastructure;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Administration;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Workers.DatabaseBackup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);
ValidateLocalDevelopmentBackupTarget(builder.Configuration, builder.Environment.EnvironmentName);
builder.Configuration["BackupController:SourceDatabaseIdentity"] = new Npgsql.NpgsqlConnectionStringBuilder(builder.Configuration.GetConnectionString("ArginTaxPayerDb") ?? "").Database;
ApplyActiveBackupDatabase(builder.Configuration);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
builder.WebHost.UseUrls(builder.Configuration["BackupController:ListenUrl"] ?? "http://127.0.0.1:15092");
builder.Services.AddArginTaxPayerApplication();
builder.Services.AddArginTaxPayerInfrastructure();
builder.Services.AddArginTaxPayerConfiguration(builder.Configuration, builder.Environment.IsProduction());
// The backup controller needs the shared DbContext but must not register the
// API's unrelated invoice services, whose optional signing dependencies live in the API host.
builder.Services.AddArginTaxPayerDbContext(builder.Configuration, builder.Environment.IsProduction());
builder.Services.AddScoped<IdentitySessionService>();
builder.Services.AddScoped<IdentityAuthorizationService>();
builder.Services.AddScoped<DatabaseBackupSettingsService>();
builder.Services.AddSingleton(_ => new ProcessBackupHostCommandRunner(
    builder.Configuration["BackupController:HostHelperPath"] ?? "/usr/local/sbin/argin-taxpayer-backup-control",
    builder.Configuration["BackupController:SudoPath"] ?? "/usr/bin/sudo",
    builder.Configuration.GetValue("BackupController:UseSudo", true)));
builder.Services.AddSingleton<IBackupHostCommandRunner>(sp => sp.GetRequiredService<ProcessBackupHostCommandRunner>());
builder.Services.AddSingleton<IBackupDatabaseSwitcher>(sp => sp.GetRequiredService<ProcessBackupHostCommandRunner>());
builder.Services.AddSingleton<ServiceLifecycle>();
builder.Services.AddSingleton<IBackupApiReadinessProbe, LoopbackBackupApiReadinessProbe>();
builder.Services.AddSingleton(new ManualServiceStopState(
    builder.Configuration["BackupController:StateDirectory"] ?? "/var/lib/argintaxpayer/backup-controller"));
builder.Services.AddSingleton<BackupBundleService>();
builder.Services.AddSingleton<BackupWorkQueue>();
builder.Services.AddHostedService<BackupWorkService>();
builder.Services.AddSingleton(new RecoveryModeStore(
    builder.Configuration["BackupController:StateDirectory"] ?? "/var/lib/argintaxpayer/backup-controller"));
builder.Services.AddScoped<RestoreCoordinator>();
builder.Services.AddSingleton<IBackupArtifactCreator, PostgresBackupArtifactCreator>();
builder.Services.AddScoped<BackupExecutionService>();
builder.Services.AddHostedService<DatabaseBackupHostedScheduler>();
builder.Services.AddSingleton(new FtpsCredentialStore(
    builder.Configuration["BackupController:FtpsSecretPath"] ?? "/etc/argintaxpayer/backup-ftps.json"));
builder.Services.AddSingleton(new BackupOperationStore(
    builder.Configuration["BackupController:StateDirectory"] ?? "/var/lib/argintaxpayer/backup-controller"));

var app = builder.Build();
app.Services.GetRequiredService<BackupBundleService>().CleanupAbandonedFiles();
app.Use(async (context, next) =>
{
    if (context.Connection.RemoteIpAddress is { } remote && !System.Net.IPAddress.IsLoopback(remote))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }
    await next();
});
app.MapGet("/health", () => Results.Ok(new { status = "ready" }));
app.MapBackupController();
var recoveryState = await app.Services.GetRequiredService<RecoveryModeStore>()
    .GetAsync(CancellationToken.None);
await app.Services.GetRequiredService<BackupOperationStore>()
    .RecoverInterruptedAsync(CancellationToken.None, recoveryState.RecoveryMode);
await app.RunAsync();

static void ApplyActiveBackupDatabase(ConfigurationManager configuration)
{
    var database = configuration["Backup:ActiveDatabase"];
    if (string.IsNullOrWhiteSpace(database)) return;
    if (!System.Text.RegularExpressions.Regex.IsMatch(database, "^recovery_[a-f0-9]{12}$"))
        throw new InvalidOperationException("Active backup database name is invalid.");
    var configured = configuration.GetConnectionString("ArginTaxPayerDb")
        ?? throw new InvalidOperationException("PostgreSQL connection is not configured.");
    var builder = new Npgsql.NpgsqlConnectionStringBuilder(configured) { Database = database };
    configuration.AddInMemoryCollection(new Dictionary<string, string?>
    { ["ConnectionStrings:ArginTaxPayerDb"] = builder.ConnectionString });
}

static void ValidateLocalDevelopmentBackupTarget(IConfiguration configuration, string environmentName)
{
    if (!configuration.GetValue("BackupController:LocalDevelopmentMode", false)) return;
    if (!string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Local development backup mode requires the Development environment.");
    var connection = configuration.GetConnectionString("ArginTaxPayerDb")
        ?? throw new InvalidOperationException("PostgreSQL connection is not configured.");
    var database = new Npgsql.NpgsqlConnectionStringBuilder(connection).Database;
    if (!string.Equals(database, "argintaxpayer_test", StringComparison.Ordinal))
        throw new InvalidOperationException("Local development backup mode is restricted to argintaxpayer_test.");
}
