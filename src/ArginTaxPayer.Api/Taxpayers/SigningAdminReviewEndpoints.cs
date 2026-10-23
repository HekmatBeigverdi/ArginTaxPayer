using ArginTaxPayer.Api.Identity;
using ArginTaxPayer.Application.Signing;

namespace ArginTaxPayer.Api.Taxpayers;

public static class SigningAdminReviewEndpoints
{
    public static IEndpointRouteBuilder MapSigningAdminReviewEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/dev/admin/signing-change-requests")
            .ExcludeFromDescription().RequireRateLimiting("signing-import");
        group.MapGet("", ListAsync);
        group.MapPost("/{requestId:guid}/decision", DecideAsync);
        return endpoints;
    }

    private static string? Bearer(HttpContext http)
    {
        var header = http.Request.Headers.Authorization.ToString();
        return header.Length <= 4096 && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header[7..].Trim() : null;
    }

    private static async Task<IResult> ListAsync(HttpContext http,
        ISigningAdminReviewWorkflow workflow, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        if (!IdentityDevelopmentEndpoints.IsLoopbackAddress(http.Connection.RemoteIpAddress))
            return Results.NotFound();
        if (http.Request.Headers.ContainsKey("Origin")) return Results.StatusCode(403);
        var records = await workflow.ListPendingAsync(Bearer(http), ct);
        return records is null ? Results.Json(new { error = "forbidden" }, statusCode: 403) :
            Results.Ok(new { requests = records });
    }

    private sealed record DecisionPayload(long ExpectedRevision, bool Approve, string DecisionReason);
    private static async Task<IResult> DecideAsync(Guid requestId, HttpContext http,
        ISigningAdminReviewWorkflow workflow, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        if (!IdentityDevelopmentEndpoints.IsLoopbackAddress(http.Connection.RemoteIpAddress))
            return Results.NotFound();
        if (http.Request.Headers.ContainsKey("Origin")) return Results.StatusCode(403);
        if (!http.Request.HasJsonContentType()) return Results.StatusCode(415);
        if (http.Request.ContentLength is > 4096) return Results.StatusCode(413);
        DecisionPayload? payload;
        try { payload = await http.Request.ReadFromJsonAsync<DecisionPayload>(cancellationToken: ct); }
        catch (System.Text.Json.JsonException) { return Results.Json(new { error = "invalid_request" }, statusCode: 400); }
        if (payload is null) return Results.Json(new { error = "invalid_request" }, statusCode: 400);
        var result = await workflow.DecideAsync(Bearer(http), new(requestId, payload.ExpectedRevision,
            payload.Approve, payload.DecisionReason), ct);
        return result.Status switch {
            SigningAdminReviewStatus.Updated => Results.Ok(result.Item),
            SigningAdminReviewStatus.Invalid => Results.Json(new { error = "invalid_request" }, statusCode: 400),
            SigningAdminReviewStatus.Forbidden or SigningAdminReviewStatus.Unauthenticated =>
                Results.Json(new { error = "forbidden" }, statusCode: 403),
            SigningAdminReviewStatus.NotFound => Results.Json(new { error = "not_found" }, statusCode: 404),
            _ => Results.Json(new { error = "conflict" }, statusCode: 409)
        };
    }
}
