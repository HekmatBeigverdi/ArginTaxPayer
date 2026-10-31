using System.Net;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using Microsoft.AspNetCore.Mvc;
namespace ArginTaxPayer.Api.Subscriptions;
public static class Phase12ActivationEndpoints
{
    public sealed record ActivatePaidOrderRequest(Guid OrderId);
    public static IEndpointRouteBuilder MapPhase12ActivationEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/dev/subscriptions/paid-orders/activate",async(
            HttpContext http,[FromBody] ActivatePaidOrderRequest input,
            CommercialSubscriptionActivationService service,CancellationToken ct)=>
        {
            var ip=http.Connection.RemoteIpAddress;
            if(ip is null || !(IPAddress.IsLoopback(ip) ||
               ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4())))
                return Results.NotFound();
            if(http.Request.Headers.ContainsKey("Origin"))return Results.StatusCode(403);
            if(http.Request.ContentLength is > 1024)return Results.StatusCode(413);
            var authorization=http.Request.Headers.Authorization.ToString();
            var bearer=authorization.Length<=4096 &&
                authorization.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase)
                ?authorization[7..].Trim():null;
            if(string.IsNullOrWhiteSpace(bearer))return Results.Unauthorized();
            try {
                var subscription=await service.ActivateFullyPaidOrderAsync(bearer,input.OrderId,
                    DateTime.UtcNow,ct);
                return Results.Json(new {subscriptionId=subscription});
            } catch(UnauthorizedAccessException){return Results.StatusCode(403);}
              catch(ArgumentException){return Results.BadRequest();}
              catch(InvalidOperationException){return Results.Conflict();}
        }).ExcludeFromDescription().RequireRateLimiting("signing-import");
        return routes;
    }
}
