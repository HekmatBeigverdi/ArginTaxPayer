using ArginTaxPayer.Api.Identity;
using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Infrastructure.Persistence.Signing;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using ArginTaxPayer.Domain.Taxpayers;
using System.Text.Json;

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
        ISigningReadOnlyConnectionTest connection, FiscalMemoryManagementService memories,
        FiscalMemoryAuthorityOnboardingService authorityOnboarding, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Headers.Pragma = "no-cache";
        if (!IdentityDevelopmentEndpoints.IsLoopbackAddress(http.Connection.RemoteIpAddress))
            return Results.NotFound();
        if (http.Request.Headers.ContainsKey("Origin")) return Results.StatusCode(403);
        var (bodyStatus, oneTimeEnvironment) = await ReadOneTimeEnvironmentAsync(http.Request, ct);
        if (bodyStatus is not null) return Results.StatusCode(bodyStatus.Value);
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
                result = await connection.TestAsync(bearer, taxpayerId, memoryId, ct, oneTimeEnvironment);
            var memoryActivated = result.Status == SigningConnectionTestStatus.Authenticated &&
                await authorityOnboarding.ActivateAfterSuccessfulConnectionTestAsync(
                    bearer, taxpayerId, memoryId, oneTimeEnvironment, result, ct);
            await memories.RecordConnectionTestAsync(bearer, taxpayerId, memoryId,
                result.Status == SigningConnectionTestStatus.Authenticated, result.TestedAtUtc, ct);
            return Results.Json(new
            {
                status = result.Status.ToString(),
                result.SafeCode,
                result.TestedAtUtc,
                result.AuthenticationConfirmed,
                result.PortalRegistrationConfirmed,
                result.FiscalMemoryActivationConfirmed,
                fiscalInformation = result.FiscalInformation,
                environmentPersisted = memoryActivated && oneTimeEnvironment is not null,
                memoryActivated,
                oneTimeTest = oneTimeEnvironment is not null,
                testEnvironment = oneTimeEnvironment?.ToString()
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

    private static async Task<(int? Status, FiscalConnectionEnvironment? Environment)> ReadOneTimeEnvironmentAsync(
        HttpRequest request, CancellationToken ct)
    {
        if (request.ContentLength is > 256) return (413, null);
        if (request.ContentLength == 0) return (null, null);
        await using var buffer = new MemoryStream();
        var chunk = new byte[257];
        while (true)
        {
            var count = await request.Body.ReadAsync(chunk, ct);
            if (count == 0) break;
            if (buffer.Length + count > 256) return (413, null);
            await buffer.WriteAsync(chunk.AsMemory(0, count), ct);
        }
        if (buffer.Length == 0) return (null, null);
        if (!string.Equals(request.ContentType?.Split(';', 2)[0].Trim(), "application/json",
            StringComparison.OrdinalIgnoreCase)) return (415, null);
        try
        {
            using var document = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 2 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 1 ||
                !root.TryGetProperty("testEnvironment", out var value) || value.ValueKind != JsonValueKind.String)
                return (400, null);
            return value.GetString() switch
            {
                "Production" => (null, FiscalConnectionEnvironment.Production),
                "Sandbox" => (null, FiscalConnectionEnvironment.Sandbox),
                _ => (400, null)
            };
        }
        catch (JsonException) { return (400, null); }
    }
}
