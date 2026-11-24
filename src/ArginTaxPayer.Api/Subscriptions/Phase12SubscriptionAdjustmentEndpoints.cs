using System.Net;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using Microsoft.AspNetCore.Mvc;

namespace ArginTaxPayer.Api.Subscriptions;

public static class Phase12SubscriptionAdjustmentEndpoints
{
    public sealed record AdjustRequest(Guid SubscriptionId, int? ValidityDays, long? InvoiceUnitLimit,
        long ExpectedRevision, string OperationKey, string Reason);
    public sealed record CancelRequest(Guid SubscriptionId, long ExpectedRevision, string OperationKey, string Reason, bool FullRefund);
    public sealed record CancelPreviewRequest(Guid SubscriptionId, long ExpectedRevision, bool FullRefund);

    public static IEndpointRouteBuilder MapPhase12SubscriptionAdjustmentEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/dev/customer-owners/{customerId:guid}/subscription-adjustment", async (
            HttpContext http, Guid customerId, SubscriptionAdjustmentService service, CancellationToken ct) =>
            await Execute(http, () => service.ReadActiveAsync(Token(http), customerId, ct), ct));
        routes.MapGet("/api/dev/customer-owners/{customerId:guid}/subscription-adjustment/history", async (
            HttpContext http, Guid customerId, SubscriptionAdjustmentService service, CancellationToken ct) =>
            await Execute(http, () => service.ReadHistoryAsync(Token(http), customerId, ct), ct));
        routes.MapPost("/api/dev/customer-owners/{customerId:guid}/subscription-cancellation/preview", async (
            HttpContext http, Guid customerId, [FromBody] CancelPreviewRequest request,
            SubscriptionAdjustmentService service, CancellationToken ct) =>
            await Execute(http, () => service.PreviewCancelAsync(Token(http), customerId,
                request.SubscriptionId, request.ExpectedRevision, request.FullRefund, ct), ct));
        routes.MapPost("/api/dev/customer-owners/{customerId:guid}/subscription-adjustment", async (
            HttpContext http, Guid customerId, [FromBody] AdjustRequest request,
            SubscriptionAdjustmentService service, CancellationToken ct) =>
            await Execute(http, () => service.AdjustAsync(Token(http), customerId, request.SubscriptionId,
                request.ValidityDays, request.InvoiceUnitLimit, request.ExpectedRevision, request.OperationKey, request.Reason, ct), ct));
        routes.MapPost("/api/dev/customer-owners/{customerId:guid}/subscription-cancellation", async (
            HttpContext http, Guid customerId, [FromBody] CancelRequest request,
            SubscriptionAdjustmentService service, CancellationToken ct) =>
            await Execute(http, async () => { var result = await service.CancelAsync(Token(http), customerId, request.SubscriptionId,
                request.ExpectedRevision, request.OperationKey, request.Reason, request.FullRefund, ct); return new { status = "cancelled", refundedIrr = result.RefundedIrr,
                remainingDays = result.RemainingDays, remainingUnits = result.RemainingUnits }; }, ct));
        return routes;
    }

    private static async Task<IResult> Execute<T>(HttpContext http, Func<Task<T>> action, CancellationToken ct)
    {
        if (!Local(http)) return Results.NotFound();
        if (http.Request.Method != "GET" && http.Request.Headers.ContainsKey("Origin")) return Results.StatusCode(403);
        if (Token(http) is null) return Results.Unauthorized();
        try { http.Response.Headers.CacheControl = "no-store"; return Results.Json(await action()); }
        catch (UnauthorizedAccessException) { return Results.StatusCode(403); }
        catch (KeyNotFoundException) { return Results.NotFound(new { error = "active_subscription_not_found" }); }
        catch (SubscriptionRevisionConflictException ex) { return Results.Conflict(new { error = "stale_subscription_revision", expectedRevision = ex.ExpectedRevision, actualRevision = ex.ActualRevision }); }
        catch (InvalidOperationException ex) when (ex.Message == "Credit account missing.") { return Results.Conflict(new { error = "subscription_credit_account_missing" }); }
        catch (InvalidOperationException ex) when (ex.Message == "New quantity is below used or reserved invoices.") { return Results.Conflict(new { error = "quantity_below_usage_or_reservations" }); }
        catch (InvalidOperationException) { return Results.Conflict(new { error = "subscription_adjustment_conflict" }); }
        catch (ArgumentException) { return Results.BadRequest(new { error = "invalid_adjustment" }); }
    }
    private static bool Local(HttpContext h) => h.Connection.RemoteIpAddress is { } ip && (IPAddress.IsLoopback(ip) || ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4()));
    private static string? Token(HttpContext h) { var v=h.Request.Headers.Authorization.ToString(); return v.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase)&&v.Length<=4096?v[7..].Trim():null; }
}
