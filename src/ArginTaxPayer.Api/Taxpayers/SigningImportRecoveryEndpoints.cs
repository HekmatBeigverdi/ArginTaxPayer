using ArginTaxPayer.Api.Identity;
using ArginTaxPayer.Application.Signing;

namespace ArginTaxPayer.Api.Taxpayers;

public static class SigningImportRecoveryEndpoints
{
    public static IEndpointRouteBuilder MapSigningImportRecoveryEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGroup("/api/dev/signing/{taxpayerId:guid}/{memoryId:guid}")
            .ExcludeFromDescription().RequireRateLimiting("signing-import")
            .MapPost("/recover-import", RecoverAsync);
        return routes;
    }

    private static async Task<IResult> RecoverAsync(Guid taxpayerId, Guid memoryId,
        HttpContext http, ISigningImportRecovery recovery, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Headers.Pragma = "no-cache";
        if (!IdentityDevelopmentEndpoints.IsLoopbackAddress(http.Connection.RemoteIpAddress))
            return Results.NotFound();
        if (http.Request.Headers.ContainsKey("Origin")) return Results.StatusCode(403);
        if (http.Request.ContentLength is > 0 || http.Request.ContentType is not null)
            return Results.StatusCode(415);
        var auth = http.Request.Headers.Authorization.ToString();
        var bearer = auth.Length <= 4096 &&
            auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? auth[7..].Trim() : null;
        if (string.IsNullOrWhiteSpace(bearer))
            return Results.Json(new { error = "unauthorized" }, statusCode: 401);
        try
        {
            var outcome = await recovery.RecoverAsync(bearer, taxpayerId, memoryId, ct);
            return outcome switch
            {
                SigningGenerationStatus.Created or SigningGenerationStatus.Replayed =>
                    Results.Json(new { status = "recovered" }),
                SigningGenerationStatus.Unauthenticated => Results.Json(new { error = "unauthorized" }, statusCode: 401),
                SigningGenerationStatus.Forbidden => Results.Json(new { error = "reauthentication_or_scope_required" }, statusCode: 403),
                SigningGenerationStatus.NotFound => Results.Json(new { error = "no_pending_import" }, statusCode: 404),
                SigningGenerationStatus.IntegrityFailure => Results.Json(new { error = "integrity_failure" }, statusCode: 409),
                SigningGenerationStatus.RecoveryRequired => Results.Json(new { error = "recovery_required" }, statusCode: 409),
                _ => Results.Json(new { error = "custody_unavailable" }, statusCode: 503)
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Results.Json(new { error = "recovery_timeout" }, statusCode: 504);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            return Results.Json(new { error = "service_unavailable" }, statusCode: 503);
        }
    }
}
