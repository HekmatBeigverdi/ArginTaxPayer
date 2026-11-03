using System.Net;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using Microsoft.AspNetCore.Mvc;

namespace ArginTaxPayer.Api.Subscriptions;

public static class Phase12CommercialOrderEndpoints
{
    public sealed record IssueRequest(Guid CustomerUserId, Guid PlanId, string IdempotencyKey);

    public static IEndpointRouteBuilder MapPhase12CommercialOrderEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/dev/subscriptions/commercial-orders", async (
            HttpContext http, [FromBody] IssueRequest request,
            CommercialOrderService service, CancellationToken ct) =>
        {
            var ip = http.Connection.RemoteIpAddress;
            if(ip is null || !(IPAddress.IsLoopback(ip) ||
               ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4())))
                return Results.NotFound();
            if(http.Request.Headers.ContainsKey("Origin")) return Results.StatusCode(403);
            if(http.Request.ContentLength is > 1024) return Results.StatusCode(413);
            var authorization=http.Request.Headers.Authorization.ToString();
            var bearer=authorization.Length<=4096 &&
                authorization.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase)
                ? authorization[7..].Trim() : null;
            if(string.IsNullOrEmpty(bearer)) return Results.Unauthorized();
            try
            {
                var snapshot=await service.IssueAsync(bearer,request.CustomerUserId,
                    request.PlanId,request.IdempotencyKey,DateTime.UtcNow,ct);
                return Results.Json(new {snapshot.OrderId,snapshot.CustomerUserId,
                    snapshot.PlanId,snapshot.PriceVersionId,snapshot.GrossAmountIrr,
                    snapshot.IssuedAtUtc,financialInvoice=true,authorityInvoice=false},
                    statusCode:201);
            }
            catch(UnauthorizedAccessException) { return Results.StatusCode(403); }
            catch(ArgumentException) { return Results.BadRequest(); }
            catch(InvalidOperationException) { return Results.Conflict(); }
        }).RequireRateLimiting("signing-import").ExcludeFromDescription();
        return routes;
    }
}
