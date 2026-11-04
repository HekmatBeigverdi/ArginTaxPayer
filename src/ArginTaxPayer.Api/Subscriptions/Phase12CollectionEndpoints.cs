using System.Net;
using ArginTaxPayer.Domain.Subscriptions;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using Microsoft.AspNetCore.Mvc;

namespace ArginTaxPayer.Api.Subscriptions;

/// <summary>Loopback-only development collection confirmation. Does not accept
/// unverified gateway callbacks; operator confirmation is explicitly audited.</summary>
public static class Phase12CollectionEndpoints
{
    public sealed record ManualCollectionRequest(Guid OrderId, Guid CustomerUserId,
        CollectionChannel Channel, long AmountIrr, string ReceiptReference);
    internal static IResult CollectionConflict(InvalidOperationException exception)
    {
        var (code, message) = exception.Message switch
        {
            "Payment reference reused with different details." => ("collection_reference_reused",
                "این مرجع رسید قبلاً برای دریافت دیگری با همین روش پرداخت ثبت شده است. مرجع یکتای رسید جدید را وارد کنید."),
            "Invalid collection amount." => ("collection_amount_exceeds_balance",
                "مبلغ دریافت از ماندهٔ فعلی سفارش بیشتر است. فهرست را بازخوانی و مبلغ را اصلاح کنید."),
            "Order not collectible." => ("collection_order_not_collectible",
                "این سفارش قابل دریافت نیست یا لغو شده است. فهرست را بازخوانی کنید."),
            "Order missing." => ("collection_order_missing",
                "سفارش یافت نشد. فهرست را بازخوانی کنید."),
            _ => (null, null)
        };
        return code is null
            ? Results.Json(new { message = "ثبت دریافت به دلیل خطای داخلی انجام نشد. دوباره تلاش کنید." }, statusCode: 500)
            : Results.Conflict(new { error = code, message });
    }

    public static IEndpointRouteBuilder MapPhase12CollectionEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/dev/subscriptions/collections/manual", async (
            HttpContext http, [FromBody] ManualCollectionRequest request,
            VerifiedCollectionService service, ILogger<VerifiedCollectionService> logger, CancellationToken ct) =>
        {
            var ip=http.Connection.RemoteIpAddress;
            if(ip is null || !(IPAddress.IsLoopback(ip) ||
                (ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4()))))
                return Results.NotFound();
            if(http.Request.Headers.ContainsKey("Origin")) return Results.StatusCode(403);
            if(http.Request.ContentLength is > 1024) return Results.StatusCode(413);
            var authorization=http.Request.Headers.Authorization.ToString();
            var bearer=authorization.Length<=4096 &&
                authorization.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase)
                ? authorization[7..].Trim() : null;
            if(string.IsNullOrWhiteSpace(bearer)) return Results.Unauthorized();
            if(request.Channel == CollectionChannel.Gateway) return Results.BadRequest();
            try
            {
                var receipt=new VerifiedCollection(request.OrderId,request.CustomerUserId,
                    request.Channel,request.AmountIrr,request.ReceiptReference,DateTime.UtcNow);
                var balance=await service.RecordManualAsync(bearer,receipt,ct);
                return Results.Json(new {balance.GrossIrr,balance.CollectedIrr,
                    balance.OutstandingIrr,status=balance.Status.ToString()});
            }
            catch(UnauthorizedAccessException) { return Results.StatusCode(403); }
            catch(ArgumentException) { return Results.BadRequest(); }
            catch(InvalidOperationException ex)
            {
                var result = CollectionConflict(ex);
                if (result is IStatusCodeHttpResult { StatusCode: 500 })
                    logger.LogError(ex, "Manual collection failed for order {OrderId}", request.OrderId);
                return result;
            }
        }).ExcludeFromDescription().RequireRateLimiting("signing-import");
        return routes;
    }
}
