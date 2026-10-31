using System.Net;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using Microsoft.AspNetCore.Mvc;

namespace ArginTaxPayer.Api.Subscriptions;

public static class Phase12SubscriptionExceptionEndpoints
{
    public sealed record ExceptionRequest(Guid ProposedTaxpayerId, Guid ProposedMemoryId, string Reason);
    public sealed record ReviewRequest(bool Approve);

    public static IEndpointRouteBuilder MapPhase12SubscriptionExceptionEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/dev/subscriptions").ExcludeFromDescription()
            .RequireRateLimiting("signing-import");
        group.MapPost("/{subscriptionId:guid}/binding-exceptions", async (
            Guid subscriptionId, [FromBody] ExceptionRequest input, HttpContext http,
            SubscriptionExceptionReviewService service, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            if (http.Request.Headers.ContainsKey("Origin")) return Results.StatusCode(403);
            if (http.Request.ContentLength is > 1500) return Results.StatusCode(413);
            try
            {
                var id = await service.RequestAsync(Bearer(http), subscriptionId,
                    input.ProposedTaxpayerId, input.ProposedMemoryId, input.Reason, ct);
                return Results.Json(new { id, state = "Pending" }, statusCode: 201);
            }
            catch (UnauthorizedAccessException) { return Results.StatusCode(403); }
            catch (ArgumentException) { return Results.BadRequest(); }
            catch (InvalidOperationException) { return Results.Conflict(); }
        });
        group.MapPost("/binding-exceptions/{requestId:guid}/review", async (
            Guid requestId, [FromBody] ReviewRequest input, HttpContext http,
            SubscriptionExceptionReviewService service, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            if (http.Request.Headers.ContainsKey("Origin")) return Results.StatusCode(403);
            if (http.Request.ContentLength is > 256) return Results.StatusCode(413);
            try
            {
                await service.ReviewAsync(Bearer(http), requestId, input.Approve, ct);
                return Results.Json(new { status = input.Approve ? "Approved" : "Rejected",
                    bindingChanged = false });
            }
            catch (UnauthorizedAccessException) { return Results.StatusCode(403); }
            catch (InvalidOperationException) { return Results.Conflict(); }
        });
        return routes;
    }

    private static bool Local(HttpContext http)
    {
        var ip = http.Connection.RemoteIpAddress;
        return ip is not null && (IPAddress.IsLoopback(ip) ||
            ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4()));
    }
    private static string? Bearer(HttpContext http)
    {
        var authorization = http.Request.Headers.Authorization.ToString();
        return authorization.Length <= 4096 &&
            authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization[7..].Trim() : null;
    }
}
