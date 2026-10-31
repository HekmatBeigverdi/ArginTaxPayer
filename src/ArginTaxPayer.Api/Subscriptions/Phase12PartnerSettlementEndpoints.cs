using System.Net;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using Microsoft.AspNetCore.Mvc;

namespace ArginTaxPayer.Api.Subscriptions;

/// <summary>Explicit internal operator recording of independently confirmed transfers.
/// This is NOT a money movement or payment initiation API.</summary>
public static class Phase12PartnerSettlementEndpoints
{
    public sealed record PaymentRecord(Guid AllocationId,long AmountIrr,string BankReference,string Reason);
    public sealed record ReversalRecord(Guid AllocationId,Guid OriginalPaymentId,string ReversalReference,string Reason);
    public static IEndpointRouteBuilder MapPhase12PartnerSettlementEndpoints(this IEndpointRouteBuilder routes)
    {
        static bool Loopback(HttpContext http)
        {
            var ip=http.Connection.RemoteIpAddress;
            return ip is not null && (IPAddress.IsLoopback(ip) ||
                ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4()));
        }
        static string? Token(HttpContext http)
        {
            var h=http.Request.Headers.Authorization.ToString();
            return h.Length<=4096 && h.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase)
                ? h[7..].Trim() : null;
        }
        routes.MapPost("/api/dev/subscriptions/settlements/record",async(
            HttpContext http,[FromBody] PaymentRecord request,PartnerSettlementService service,
            CancellationToken ct)=>
        {
            if(!Loopback(http))return Results.NotFound();
            if(http.Request.Headers.ContainsKey("Origin"))return Results.StatusCode(403);
            if(http.Request.ContentLength is > 2048)return Results.StatusCode(413);
            var token=Token(http);
            if(string.IsNullOrWhiteSpace(token))return Results.Unauthorized();
            try
            {
                var id=await service.RecordPaymentAsync(token,request.AllocationId,
                    request.AmountIrr,request.BankReference,request.Reason,ct);
                return Results.Json(new {settlementEventId=id});
            }
            catch(UnauthorizedAccessException){return Results.StatusCode(403);}
            catch(ArgumentException){return Results.BadRequest();}
            catch(InvalidOperationException){return Results.Conflict();}
        }).ExcludeFromDescription().RequireRateLimiting("signing-import");
        routes.MapPost("/api/dev/subscriptions/settlements/reverse",async(
            HttpContext http,[FromBody] ReversalRecord request,PartnerSettlementService service,
            CancellationToken ct)=>
        {
            if(!Loopback(http))return Results.NotFound();
            if(http.Request.Headers.ContainsKey("Origin"))return Results.StatusCode(403);
            if(http.Request.ContentLength is > 2048)return Results.StatusCode(413);
            var token=Token(http);
            if(string.IsNullOrWhiteSpace(token))return Results.Unauthorized();
            try
            {
                var id=await service.ReversePaymentAsync(token,request.AllocationId,
                    request.OriginalPaymentId,request.ReversalReference,request.Reason,ct);
                return Results.Json(new {reversalEventId=id});
            }
            catch(UnauthorizedAccessException){return Results.StatusCode(403);}
            catch(ArgumentException){return Results.BadRequest();}
            catch(InvalidOperationException){return Results.Conflict();}
        }).ExcludeFromDescription().RequireRateLimiting("signing-import");
        return routes;
    }
}
