using ArginTaxPayer.Domain.Administration;
using ArginTaxPayer.Infrastructure.Persistence.Administration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;

namespace ArginTaxPayer.Workers.DatabaseBackup;

public static class BackupBundleEndpoints
{
    public static void MapBackupBundles(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/backup/artifacts/download/{id:guid}", async (Guid id, HttpContext context,
            DatabaseBackupSettingsService settings, BackupBundleService bundles, CancellationToken ct) =>
        {
            try
            {
                var actor = await settings.RequireRootAsync(Bearer(context), ct);
                var policy = await settings.ReadForRootAsync(actor, ct) ?? DatabaseBackupSettings.Default;
                var stream = await bundles.DownloadAsync(policy, id, ct);
                context.Response.Headers.CacheControl = "private, no-store";
                return Results.File(stream, "application/zip", stream.FileName);
            }
            catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
            catch (BackupBundleException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });
        endpoints.MapPost("/api/backup/artifacts/upload", async (HttpContext context, DatabaseBackupSettingsService settings,
            BackupBundleService bundles, RestoreCoordinator restore, BackupOperationStore operations,
            BackupWorkQueue queue, CancellationToken ct) =>
        {
            string? path = null;
            try
            {
                var actor = await settings.RequireRootAsync(Bearer(context), ct);
                if ((await restore.GetRecoveryStateAsync(ct)).RecoveryMode)
                    return Results.Conflict(new { error = "restore_review_required" });
                if ((await operations.GetStatusAsync(ct))?.Status is "running" or "queued")
                    return Results.Conflict(new { error = "backup_operation_in_progress" });
                if (context.Request.ContentType != "application/zip")
                    return Results.BadRequest(new { error = "backup_bundle_zip_required" });
                if (context.Request.ContentLength > bundles.MaximumBytes)
                    return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                var sizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
                if (sizeFeature is { IsReadOnly: false }) sizeFeature.MaxRequestBodySize = bundles.MaximumBytes;
                var policy = await settings.ReadForRootAsync(actor, ct) ?? DatabaseBackupSettings.Default;
                path = await bundles.ReceiveAsync(context.Request.Body, ct);
                if ((await restore.GetRecoveryStateAsync(ct)).RecoveryMode)
                    return Results.Conflict(new { error = "restore_review_required" });
                var result = await BackupControllerEndpoints.QueueOperationAsync(operations, queue,
                    new BackupWorkItem("import-zip", Guid.Empty, actor, policy, BundlePath: path), "import-zip", "validate-only", ct);
                if (result is IStatusCodeHttpResult { StatusCode: 202 }) path = null;
                return result;
            }
            catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
            catch (BackupBundleException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (InvalidOperationException) { return Results.Conflict(new { error = "backup_operation_in_progress" }); }
            finally { if (path is not null) File.Delete(path); }
        });
    }
    private static string? Bearer(HttpContext context)
    {
        var value = context.Request.Headers.Authorization.ToString();
        return value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? value[7..] : null;
    }
}
