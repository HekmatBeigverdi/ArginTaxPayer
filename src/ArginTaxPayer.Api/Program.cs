using ArginTaxPayer.Api.Health;
using ArginTaxPayer.Api.Identity;
using ArginTaxPayer.Api.Taxpayers;
using ArginTaxPayer.Api.Parties;
using ArginTaxPayer.Api.Customers;
using ArginTaxPayer.Infrastructure.Persistence.Customers;
using ArginTaxPayer.Infrastructure.Persistence.Parties;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Api.Observability;
using ArginTaxPayer.Application;
using ArginTaxPayer.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var identityDevelopmentEnabled = builder.Environment.IsDevelopment() &&
    !string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("ArginTaxPayerDb"));
// JSON console output is machine-readable on macOS, Ubuntu and future containers.
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
builder.Services.AddArginTaxPayerApplication();
builder.Services.AddArginTaxPayerInfrastructure();
builder.Services.AddArginTaxPayerConfiguration(builder.Configuration, builder.Environment.IsProduction());
builder.Services.AddArginTaxPayerPersistence(builder.Configuration, builder.Environment.IsProduction());

// OpenAPI document generation is registered for API contract development.
builder.Services.AddOpenApi();
if (identityDevelopmentEnabled)
{
    builder.Services.AddSingleton<IdentityProtectionService>();
    builder.Services.AddScoped<IdentityCredentialService>();
    builder.Services.AddScoped<IdentityAdministratorUserProvisioning>();
    builder.Services.AddScoped<IdentitySessionService>();
    if (builder.Configuration["Identity:OtpDeliveryMode"] == "LocalFile")
        builder.Services.AddScoped<IIdentityOtpSender, LocalFileOtpSender>();
    else
        builder.Services.AddHttpClient<IIdentityOtpSender, GhasedakOtpSender>(client =>
            client.Timeout = TimeSpan.FromSeconds(12));
    builder.Services.AddScoped<IdentityPhoneService>();
    builder.Services.AddScoped<IdentityAuthorizationService>();
    builder.Services.AddScoped<CustomerSelfServiceService>();
    builder.Services.AddScoped<CustomerTaxpayerMembershipService>();
    builder.Services.AddScoped<CustomerAccountProfileService>();
    builder.Services.AddScoped<PartyDirectoryService>();
    builder.Services.AddScoped<NaturalPersonService>();
    builder.Services.AddScoped<LegalEntityService>();
    builder.Services.AddScoped<PartyContactAddressService>();
    builder.Services.AddScoped<PartyDeduplicationService>();
    builder.Services.AddScoped<TaxpayerCreationService>();
    builder.Services.AddScoped<TaxpayerProfileLifecycleService>();
    builder.Services.AddScoped<FiscalMemoryManagementService>();
    builder.Services.AddScoped<CustomerCommercialOwnershipService>();
    builder.Services.AddScoped<TaxpayerScopeService>();
    builder.Services.AddScoped<FiscalCertificateMetadataService>();
    builder.Services.AddScoped<IIdentityResourceOwnershipResolver, IdentityResourceOwnershipResolver>();
    builder.Services.AddScoped<IdentityResourceAuthorizationService>();
    var mfaEncryptionKey = builder.Configuration["Identity:MfaEncryptionKey"];
    if (!string.IsNullOrWhiteSpace(mfaEncryptionKey))
    {
        builder.Services.AddSingleton(new IdentityMfaCrypto(mfaEncryptionKey));
        builder.Services.AddScoped<IdentityMfaService>();
    }
}

// Separate cheap process liveness from real PostgreSQL readiness.
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<DatabaseReadinessHealthCheck>("postgresql", tags: ["ready"]);

var app = builder.Build();
app.UseMiddleware<CorrelationIdMiddleware>();

// Never expose the API contract endpoint in Production by default.
if (app.Environment.IsDevelopment())
{
    // Diagnostic deliberately contains no configuration values, credentials or IPs.
    // Registered even when development identity endpoints were not mapped.
    app.MapGet("/health/dev-identity", (HttpContext http) =>
    {
        if (!IdentityDevelopmentEndpoints.IsLoopbackAddress(http.Connection.RemoteIpAddress))
            return Results.NotFound();
        return Results.Ok(new
        {
            development = true,
            connectionConfigured = !string.IsNullOrWhiteSpace(
                app.Configuration.GetConnectionString("ArginTaxPayerDb")),
            identityEndpointsRegistered = identityDevelopmentEnabled,
            registrationEnabled = app.Configuration.GetValue<bool>(
                "Identity:AllowDevelopmentRegistration"),
            smsSendingEnabled = app.Configuration.GetValue<bool>("Ghasedak:EnableLiveSending"),
            mfaConfigured = !string.IsNullOrWhiteSpace(app.Configuration["Identity:MfaEncryptionKey"]),
            ghasedakApiKeyConfigured = !string.IsNullOrWhiteSpace(app.Configuration["Ghasedak:ApiKey"]),
            ghasedakTemplateConfigured = app.Configuration["Ghasedak:TemplateName"] == "TaxPayerActivationCode",
            ghasedakLoginTemplateConfigured = true,
            ghasedakRecipientRestrictionEnabled = app.Configuration.GetValue("Ghasedak:RestrictToTestRecipient", true),
            ghasedakTestRecipientConfigured = ArginTaxPayer.Infrastructure.Persistence.Identity.IranianMobileNumber.TryNormalize(
                app.Configuration["Ghasedak:TestRecipientMobile"], out _),
            otpPepperConfigured = (app.Configuration["Identity:OtpPepper"]?.Length ?? 0) >= 32,
            localOtpTestMode = app.Configuration["Identity:OtpDeliveryMode"] == "LocalFile",
            loopbackRecognized = true
        });
    }).ExcludeFromDescription();
    app.MapOpenApi();
    if (identityDevelopmentEnabled)
    {
        app.MapIdentityDevelopmentEndpoints();
        app.MapTaxpayerDevelopmentEndpoints();
        app.MapPartyDevelopmentEndpoints();
        app.MapCustomerProfileDevelopmentEndpoints();
        app.MapCustomerTaxpayerMembershipEndpoints();
        app.MapCustomerSelfServiceEndpoints();
    }
}

// Preserve the existing bootstrap endpoint.
app.MapGet("/", () => Results.Ok(new { application = "ArginTaxPayer", stage = "bootstrap" }))
    .WithName("GetBootstrapStatus")
    .WithTags("Platform");

// Minimal responses by design: never return raw connection strings or exceptions.
static Task WriteHealthResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    return JsonSerializer.SerializeAsync(context.Response.Body,
        new { status = report.Status.ToString() },
        cancellationToken: context.RequestAborted);
}

static async Task RunHealthCheckAsync(
    HttpContext context,
    HealthCheckService healthCheckService,
    string tag)
{
    var report = await healthCheckService.CheckHealthAsync(
        registration => registration.Tags.Contains(tag),
        context.RequestAborted);
    context.Response.StatusCode = report.Status == HealthStatus.Healthy
        ? StatusCodes.Status200OK
        : StatusCodes.Status503ServiceUnavailable;
    await WriteHealthResponse(context, report);
}

app.MapGet("/health/live", (HttpContext context, HealthCheckService healthChecks) =>
        RunHealthCheckAsync(context, healthChecks, "live"))
    .WithName("GetLiveness");

app.MapGet("/health/ready", (HttpContext context, HealthCheckService healthChecks) =>
        RunHealthCheckAsync(context, healthChecks, "ready"))
    .WithName("GetReadiness");

app.Run();

public partial class Program { }
