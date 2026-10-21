using ArginTaxPayer.Api.Identity;
using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Infrastructure.Persistence.Signing;

namespace ArginTaxPayer.Api.Taxpayers;

/// <summary>
/// Read-only, owner-scoped connection preflight. Never suggests that a Vault/key
/// preflight constitutes authenticated connection to the tax authority.
/// </summary>
public static class SigningConnectionEndpoints
{
    public static IEndpointRouteBuilder MapSigningConnectionEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGroup("/api/dev/signing/{taxpayerId:guid}/{memoryId:guid}")
            .ExcludeFromDescription().RequireRateLimiting("signing-import")
            .MapPost("/connection-test", TestAsync);
        return routes;
    }

    internal static async Task<IResult> TestAsync(Guid taxpayerId, Guid memoryId,
        HttpContext http, EfSigningOperationRepository journal,
        ISigningReadOnlyConnectionTest connection, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Headers.Pragma = "no-cache";
        if (!IdentityDevelopmentEndpoints.IsLoopbackAddress(http.Connection.RemoteIpAddress))
            return Results.NotFound();
        if (http.Request.Headers.ContainsKey("Origin")) return Results.StatusCode(403);
        if (http.Request.ContentLength is > 0) return Results.StatusCode(413);
        if (http.Request.ContentType is not null) return Results.StatusCode(415);
        var authorization = http.Request.Headers.Authorization.ToString();
        var bearer = authorization.Length <= 4096 &&
            authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization[7..].Trim() : null;
        if (string.IsNullOrEmpty(bearer)) return Results.Json(
            new { error = "unauthorized" }, statusCode: 401);
        try
        {
            var state = await journal.ReadImportOnboardingAsync(bearer, taxpayerId, memoryId, ct);
            if (state.Status == SigningGenerationStatus.Unauthenticated)
                return Results.Json(new { error = "unauthorized" }, statusCode: 401);
            if (state.Status != SigningGenerationStatus.Replayed)
                return Results.Json(new { error = "forbidden" }, statusCode: 403);
            SigningConnectionTestResult result;
            if (!state.HasKey || state.LifecycleState != 2)
                result = SigningConnectionTestResult.Create(SigningConnectionTestStatus.KeyNotReady, DateTime.UtcNow);
            else
                result = await connection.TestAsync(bearer, taxpayerId, memoryId, ct);
            return Results.Json(new
            {
                status = result.Status.ToString(),
                result.SafeCode,
                result.TestedAtUtc,
                result.AuthenticationConfirmed,
                result.PortalRegistrationConfirmed,
                result.FiscalMemoryActivationConfirmed
            }, statusCode: 200);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Results.Json(new { error = "authority_timeout" }, statusCode: 504);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            return Results.Json(new { error = "service_unavailable" }, statusCode: 503);
        }
    }
}
