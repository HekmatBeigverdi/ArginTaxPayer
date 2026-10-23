using ArginTaxPayer.Api.Identity;
using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Signing;
using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Api.Taxpayers;

/// <summary>Step23: submission and read-only list; review and token recreation remain Step24.</summary>
public static class SigningChangeRequestEndpoints
{
    public static IEndpointRouteBuilder MapSigningChangeRequestEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/dev/signing/{taxpayerId:guid}/{memoryId:guid}/change-requests")
            .ExcludeFromDescription().RequireRateLimiting("signing-import");
        group.MapGet("", ListAsync);
        group.MapPost("", SubmitAsync);
        return routes;
    }

    private static string? Bearer(HttpContext http)
    {
        var value = http.Request.Headers.Authorization.ToString();
        return value.Length <= 4096 && value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? value[7..].Trim() : null;
    }

    private static async Task<IResult> ListAsync(Guid taxpayerId, Guid memoryId,
        HttpContext http, ISigningChangeRequestSubmission service,
        ArginTaxPayerDbContext db, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        if (!IdentityDevelopmentEndpoints.IsLoopbackAddress(http.Connection.RemoteIpAddress))
            return Results.NotFound();
        if (http.Request.Headers.ContainsKey("Origin")) return Results.StatusCode(403);
        var requests = await service.ListAsync(Bearer(http), taxpayerId, memoryId, ct);
        if (requests is null) return Results.Json(new { error = "forbidden" }, statusCode: 403);
        var revision = await db.SigningKeyVersions.AsNoTracking()
            .Where(k => k.TaxpayerId == taxpayerId && k.FiscalMemoryId == memoryId &&
                k.LifecycleState == 2)
            .OrderByDescending(k => k.VersionNumber).Select(k => (long?)k.Revision).FirstOrDefaultAsync(ct);
        return Results.Ok(new { currentKeyRevision = revision, requests });
    }

    private static async Task<IResult> SubmitAsync(Guid taxpayerId, Guid memoryId,
        HttpContext http, ISigningChangeRequestSubmission service, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        if (!IdentityDevelopmentEndpoints.IsLoopbackAddress(http.Connection.RemoteIpAddress))
            return Results.NotFound();
        if (http.Request.Headers.ContainsKey("Origin")) return Results.StatusCode(403);
        if (!http.Request.HasJsonContentType()) return Results.StatusCode(415);
        if (http.Request.ContentLength is > 4096) return Results.StatusCode(413);
        try
        {
            var input = await http.Request.ReadFromJsonAsync<SigningChangeRequestInput>(
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web), ct);
            if (input is null) return Results.Json(new { error = "invalid_request" }, statusCode: 400);
            var result = await service.SubmitAsync(Bearer(http), taxpayerId, memoryId, input, ct);
            var error = result.Status switch
            {
                SigningChangeSubmissionStatus.Invalid => "invalid_request",
                SigningChangeSubmissionStatus.Forbidden => "forbidden",
                SigningChangeSubmissionStatus.NotFound => "not_found",
                SigningChangeSubmissionStatus.SecondMemoryDenied => "second_memory_requires_plan_review",
                SigningChangeSubmissionStatus.KeyNotReady => "key_not_ready",
                _ => "conflict"
            };
            return result.Status switch
            {
                SigningChangeSubmissionStatus.Created => Results.Json(result.Request, statusCode: 201),
                SigningChangeSubmissionStatus.Replayed => Results.Json(result.Request, statusCode: 200),
                SigningChangeSubmissionStatus.Invalid => Results.Json(new { error }, statusCode: 400),
                SigningChangeSubmissionStatus.Forbidden => Results.Json(new { error }, statusCode: 403),
                SigningChangeSubmissionStatus.NotFound => Results.Json(new { error }, statusCode: 404),
                _ => Results.Json(new { error }, statusCode: 409)
            };
        }
        catch (System.Text.Json.JsonException)
        { return Results.Json(new { error = "invalid_request" }, statusCode: 400); }
        catch (BadHttpRequestException)
        { return Results.Json(new { error = "invalid_request" }, statusCode: 400); }
    }
}
