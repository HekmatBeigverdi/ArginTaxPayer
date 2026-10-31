using System.Net;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using Microsoft.AspNetCore.Mvc;

namespace ArginTaxPayer.Api.Subscriptions;

public static class Phase12PriceEndpoints
{
    public sealed record PriceRequest(Guid PlanId,long PriceIrr,DateTime EffectiveFromUtc,DateTime? EffectiveUntilUtc);
    public sealed record ActivateRequest(Guid PlanId);
    public static IEndpointRouteBuilder MapPhase12PriceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        static bool Allowed(HttpContext ctx)
        {
            var ip=ctx.Connection.RemoteIpAddress;
            return ip is not null && (IPAddress.IsLoopback(ip) ||
              ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4())) &&
              !ctx.Request.Headers.ContainsKey("Origin") && ctx.Request.ContentLength is not > 2048;
        }
        static string? Bearer(HttpContext ctx)
        {
            var h=ctx.Request.Headers.Authorization.ToString();
            return h.Length<=4096 && h.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase)
                ?h[7..].Trim():null;
        }
        endpoints.MapPost("/api/dev/subscriptions/prices",async(
            HttpContext http,[FromBody] PriceRequest data,SubscriptionPriceAdministrationService service,
            CancellationToken ct)=>
        {
            if(!Allowed(http))return Results.StatusCode(403);
            var token=Bearer(http);
            if(string.IsNullOrEmpty(token))return Results.Unauthorized();
            try {
                var result=await service.CreateAsync(token,data.PlanId,data.PriceIrr,
                    data.EffectiveFromUtc,data.EffectiveUntilUtc,ct);
                return Results.Json(new{result.Id,result.PlanId,result.PartnerUserId,
                    result.PriceIrr,result.EffectiveFromUtc},statusCode:201);
            } catch(UnauthorizedAccessException){return Results.StatusCode(403);}
              catch(ArgumentException){return Results.BadRequest();}
              catch(InvalidOperationException){return Results.Conflict();}
        }).RequireRateLimiting("signing-import").ExcludeFromDescription();
        endpoints.MapPost("/api/dev/subscriptions/plans/activate",async(
            HttpContext http,[FromBody] ActivateRequest data,SubscriptionPriceAdministrationService service,
            CancellationToken ct)=>
        {
            if(!Allowed(http))return Results.StatusCode(403);
            var token=Bearer(http);
            if(string.IsNullOrEmpty(token))return Results.Unauthorized();
            try {
                var result=await service.ActivatePlanAsync(token,data.PlanId,ct);
                return Results.Json(new{result.Id,result.IsActive});
            } catch(UnauthorizedAccessException){return Results.StatusCode(403);}
              catch(InvalidOperationException){return Results.Conflict();}
        }).RequireRateLimiting("signing-import").ExcludeFromDescription();
        return endpoints;
    }
}
