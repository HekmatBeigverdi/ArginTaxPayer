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
    public static IEndpointRouteBuilder MapPhase12CollectionEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/dev/subscriptions/collections/manual", async (
            HttpContext http, [FromBody] ManualCollectionRequest request,
            VerifiedCollectionService service, CancellationToken ct) =>
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
            catch(InvalidOperationException) { return Results.Conflict(); }
        }).ExcludeFromDescription().RequireRateLimiting("signing-import");
        return routes;
    }
}
