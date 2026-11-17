using ArginTaxPayer.Domain.Administration;
using ArginTaxPayer.Infrastructure.Persistence.Administration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace ArginTaxPayer.Workers.DatabaseBackup;

public sealed record FtpsPasswordRequest(string Password);
public sealed record RestoreStageRequest(Guid ArtifactId);
public sealed record RemoteRestoreStageRequest(string Name);
public sealed record RestoreActivationRequest(Guid StageId, string Confirmation);
public sealed record RestoreEnableRequest(string Confirmation);

public static class BackupControllerEndpoints
{
    public static IEndpointRouteBuilder MapBackupController(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/backup");
        group.MapGet("/settings", async (HttpContext context, DatabaseBackupSettingsService settings,
            FtpsCredentialStore credentials,
            CancellationToken ct) =>
        {
            try
            {
                var policy = await settings.GetAsync(Bearer(context), ct);
                return Results.Ok(policy with { FtpPasswordConfigured = await IsPasswordConfiguredForAsync(policy, credentials, ct) });
            }
            catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
        });
        group.MapPut("/settings", async (HttpContext context, DatabaseBackupSettingsUpdate update,
            DatabaseBackupSettingsService settings, FtpsCredentialStore credentials, CancellationToken ct) =>
        {
            try
            {
                var policy = await settings.SaveAsync(Bearer(context), update, ct);
                return Results.Ok(policy with { FtpPasswordConfigured = await IsPasswordConfiguredForAsync(policy, credentials, ct) });
            }
            catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });
        group.MapPut("/ftps-password", async (HttpContext context, FtpsPasswordRequest request,
            DatabaseBackupSettingsService settings, FtpsCredentialStore credentials, CancellationToken ct) =>
        {
            try
            {
                var policy = await settings.GetAsync(Bearer(context), ct);
                await credentials.SaveAsync(policy.FtpUsername, request.Password, ct);
                return Results.Ok(new { passwordConfigured = true });
            }
            catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });
        group.MapGet("/operation", async (HttpContext context, DatabaseBackupSettingsService settings,
            BackupOperationStore operations, ManualServiceStopState manualStop, CancellationToken ct) =>
        {
            try
            {
                await settings.GetAsync(Bearer(context), ct);
                var current = await operations.GetStatusAsync(ct);
                var stopped = await manualStop.GetAsync(ct);
                return Results.Ok(new { status = current?.Status, kind = current?.Kind, mode = current?.Mode,
                    message = current?.Message, operationId = current?.Id, manualStartRequired = stopped.RestartRequired,
                    currentStage = current?.CurrentStage, completedSteps = current?.CompletedSteps ?? 0,
                    totalSteps = current?.TotalSteps ?? 0, progressPercent = current?.ProgressPercent ?? 0 });
            }
            catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
        });
        group.MapPost("/operations/manual", async (HttpContext context, DatabaseBackupSettingsService settings,
            RestoreCoordinator restore, BackupOperationStore operations, BackupWorkQueue queue, CancellationToken ct) =>
        {
            try
            {
                var actor = await settings.RequireRootAsync(Bearer(context), ct);
                if ((await restore.GetRecoveryStateAsync(ct)).RecoveryMode)
                    return Results.Conflict(new { error = "restore_review_required" });
                // The actor was just authenticated above; avoid repeating the same
                // remote database session and MFA checks on this queued operation path.
                var policy = await settings.ReadForRootAsync(actor, ct)
                    ?? DatabaseBackupSettings.Default;
                return await QueueOperationAsync(operations, queue,
                    new BackupWorkItem("manual", Guid.Empty, actor, policy), "manual", "maintenance", ct);
            }
            catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        });
        group.MapPost("/services/start", async (HttpContext context, DatabaseBackupSettingsService settings,
            BackupExecutionService execution, RestoreCoordinator restore, BackupOperationStore operations,
            ManualServiceStopState manualStop, CancellationToken ct) =>
        {
            try
            {
                await settings.RequireRootAsync(Bearer(context), ct);
                if ((await restore.GetRecoveryStateAsync(ct)).RecoveryMode) return Results.Conflict(new { error = "restore_review_required" });
                var restart = await manualStop.GetAsync(ct);
                var operation = await operations.GetStatusAsync(ct);
                if (!restart.RestartRequired || operation?.Status is "running" or "queued")
                    return Results.Conflict(new { error = "no_completed_manual_backup" });
                await execution.StartBusinessServicesAsync(ct);
                return Results.Ok(new { status = "started" });
            }
            catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        });
        group.MapGet("/artifacts/remote", async (HttpContext context, DatabaseBackupSettingsService settings,
            BackupExecutionService execution, CancellationToken ct) =>
        {
            try
            {
                var policy = await settings.GetAsync(Bearer(context), ct);
                return Results.Ok(new { path = policy.RemoteDirectory, files = await execution.ListRemoteAsync(policy, ct) });
            }
            catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
            catch (System.Security.Authentication.AuthenticationException)
            { return Results.Json(new { error = "ftps_tls_authentication_failed" }, statusCode: StatusCodes.Status502BadGateway); }
            catch { return Results.Json(new { error = "ftps_remote_connection_failed" }, statusCode: StatusCodes.Status502BadGateway); }
        });
        group.MapPost("/restore/stage-local", async (HttpContext context, RestoreStageRequest request,
            DatabaseBackupSettingsService settings, BackupOperationStore operations, BackupWorkQueue queue, CancellationToken ct) =>
        {
            try
            {
                var actor = await settings.RequireRootAsync(Bearer(context), ct);
                var policy = await settings.GetAsync(Bearer(context), ct);
                return await QueueOperationAsync(operations, queue,
                    new BackupWorkItem("restore-local", Guid.Empty, actor, policy, ArtifactId: request.ArtifactId),
                    "restore-stage", "isolated", ct);
            }
            catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
            catch (FileNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
            catch (InvalidDataException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });
        group.MapPost("/restore/stage-remote", async (HttpContext context, RemoteRestoreStageRequest request,
            DatabaseBackupSettingsService settings, BackupOperationStore operations, BackupWorkQueue queue, CancellationToken ct) =>
        {
            try
            {
                var actor = await settings.RequireRootAsync(Bearer(context), ct);
                var policy = await settings.GetAsync(Bearer(context), ct);
                return await QueueOperationAsync(operations, queue,
                    new BackupWorkItem("restore-remote", Guid.Empty, actor, policy, RemoteName: request.Name),
                    "restore-stage", "isolated", ct);
            }
            catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (FileNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
            catch (InvalidDataException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });
        group.MapPost("/restore/activate", async (HttpContext context, RestoreActivationRequest request,
            IConfiguration configuration,
            DatabaseBackupSettingsService settings, BackupOperationStore operations, BackupWorkQueue queue, CancellationToken ct) =>
        {
            try
            {
                var actor = await settings.RequireRootAsync(Bearer(context), ct);
                if (configuration.GetValue("BackupController:LocalDevelopmentMode", false))
                    return Results.Conflict(new { error = "restore_activation_requires_ubuntu" });
                var policy = await settings.GetAsync(Bearer(context), ct);
                return await QueueOperationAsync(operations, queue,
                    new BackupWorkItem("restore-activate", Guid.Empty, actor, policy, StageId: request.StageId, Confirmation: request.Confirmation),
                    "restore-activate", "recovery-only", ct);
            }
            catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        });
        group.MapGet("/recovery", async (HttpContext context, DatabaseBackupSettingsService settings,
            RestoreCoordinator restore, CancellationToken ct) =>
        {
            try
            {
                await settings.GetAsync(Bearer(context), ct);
                return Results.Ok(await restore.GetRecoveryStateAsync(ct));
            }
            catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
        });
        group.MapPost("/recovery/enable-business", async (HttpContext context, RestoreEnableRequest request,
            DatabaseBackupSettingsService settings, RestoreCoordinator restore, CancellationToken ct) =>
        {
            try
            {
                await settings.RequireRootAsync(Bearer(context), ct);
                await restore.EnableBusinessModeAsync(request.Confirmation, ct);
                return Results.Ok(new { recoveryMode = false });
            }
            catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        });
        group.MapGet("/artifacts/local", async (HttpContext context, DatabaseBackupSettingsService settings,
            IConfiguration configuration, CancellationToken ct) =>
        {
            try
            {
                var policy = await settings.GetAsync(Bearer(context), ct);
                return Results.Ok(new { path = policy.LocalPath, files = (await new LocalBackupStore(policy.LocalPath).ListAsync(ct))
                    .Select(file => file with { DatabaseName = BackupDatabaseIdentity.DisplayName(file.DatabaseName, configuration) }),
                    canStageRestore = !string.IsNullOrWhiteSpace(configuration.GetConnectionString("DatabaseBackupRestore")),
                    canActivateRestore = !configuration.GetValue("BackupController:LocalDevelopmentMode", false) });
            }
            catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
        });
        endpoints.MapBackupBundles();
        return endpoints;
    }

    private static async Task<bool> IsPasswordConfiguredForAsync(DatabaseBackupSettings policy,
        FtpsCredentialStore credentials, CancellationToken ct)
    {
        var stored = await credentials.ReadAsync(ct);
        return stored is not null && string.Equals(stored.Username, policy.FtpUsername, StringComparison.Ordinal);
    }

    internal static async Task<IResult> QueueOperationAsync(BackupOperationStore operations, BackupWorkQueue queue,
        BackupWorkItem item, string kind, string mode, CancellationToken ct)
    {
        var plan = BackupProgressPlan.ForQueued(item);
        var operation = await operations.StartAsync(kind, item.ActorId, mode, ct,
            plan.FirstStage, plan.TotalSteps, queued: true);
        if (!queue.TryEnqueue(item with { OperationId = operation.Id }))
        {
            await operations.CompleteAsync(operation.Id, false, "The backup controller queue is full.", CancellationToken.None);
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        return Results.Accepted(value: new { operationId = operation.Id, status = "queued" });
    }

    private static string? Bearer(HttpContext context)
    {
        var value = context.Request.Headers.Authorization.ToString();
        return value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? value[7..] : null;
    }
}
