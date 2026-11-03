using System.Net;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using Microsoft.AspNetCore.Mvc;

namespace ArginTaxPayer.Api.Subscriptions;

public static class Phase12PriceEndpoints
{
    public sealed record PriceRequest(Guid PlanId,long PriceIrr);
    public sealed record ActivateRequest(Guid PlanId);
    public sealed record PriceHistoryQuery(Guid PlanId, bool OwnPrice, Guid? PartnerUserId);
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
        static string PriceConflictMessage(string reason) => reason switch
        {
            "Current price window cannot be replaced at this instant." => "تعرفه همین لحظه در حال تغییر است؛ فهرست را بازخوانی و دوباره تلاش کنید.",
            "Price scope has overlapping current versions." => "بازه‌های تعرفهٔ ثبت‌شده هم‌پوشانی دارند؛ با پشتیبانی تماس بگیرید.",
            "Partner root scope ambiguous." => "برای این مدیر سطح ۲ یک مدیر سطح ۱ یکتا ثبت نشده است.",
            _ => "وضعیت تعرفه یا پلن تغییر کرده است؛ فهرست را بازخوانی و دوباره تلاش کنید."
        };
        endpoints.MapPost("/api/dev/subscriptions/prices",async(
            HttpContext http,[FromBody] PriceRequest data,SubscriptionPriceAdministrationService service,
            CancellationToken ct)=>
        {
            if(!Allowed(http))return Results.StatusCode(403);
            var token=Bearer(http);
            if(string.IsNullOrEmpty(token))return Results.Unauthorized();
            try {
                var result=await service.CreateAsync(token,data.PlanId,data.PriceIrr,
                    DateTime.UtcNow,null,ct);
                return Results.Json(new{result.Id,result.PlanId,result.PartnerUserId,
                    result.PriceIrr,result.EffectiveFromUtc,result.EffectiveUntilUtc},statusCode:201);
            } catch(UnauthorizedAccessException){return Results.StatusCode(403);}
              catch(ArgumentException){return Results.BadRequest();}
              catch(KeyNotFoundException){return Results.NotFound();}
              catch(InvalidOperationException ex){return Results.Conflict(new{error=PriceConflictMessage(ex.Message)});}
        }).RequireRateLimiting("signing-import").ExcludeFromDescription();
        endpoints.MapGet("/api/dev/subscriptions/prices/history",async(
            HttpContext http,[FromQuery] Guid planId,[FromQuery] bool ownPrice,
            [FromQuery] Guid? partnerUserId,[FromQuery] int? page,
            SubscriptionPriceAdministrationService service,
            CancellationToken ct)=>
        {
            if(!Allowed(http))return Results.StatusCode(403);
            var token=Bearer(http);
            if(string.IsNullOrEmpty(token))return Results.Unauthorized();
            try {
                http.Response.Headers.CacheControl="no-store";
                if(page is { } requested)
                    return Results.Json(await service.HistoryPageAsync(token,planId,ownPrice,partnerUserId,requested,ct));
                return Results.Json(await service.HistoryAsync(token,planId,ownPrice,partnerUserId,ct));
            } catch(UnauthorizedAccessException){return Results.StatusCode(403);}
              catch(ArgumentException){return Results.BadRequest();}
              catch(KeyNotFoundException){return Results.NotFound();}
              catch(InvalidOperationException ex){return Results.Conflict(new{error=PriceConflictMessage(ex.Message)});}
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
              catch(KeyNotFoundException){return Results.NotFound();}
              catch(InvalidOperationException ex){return Results.Conflict(new{error=PriceConflictMessage(ex.Message)});}
        }).RequireRateLimiting("signing-import").ExcludeFromDescription();
        return endpoints;
    }
}
