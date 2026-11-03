using System.Net;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using Microsoft.AspNetCore.Mvc;
namespace ArginTaxPayer.Api.Subscriptions;
public static class Phase12CustomerCheckoutEndpoints
{
    public sealed record CashRequest(Guid PlanId,bool WantsOfficialInvoice,string OperationKey);
    public static IEndpointRouteBuilder MapPhase12CustomerCheckoutEndpoints(this IEndpointRouteBuilder routes)
    {
        static bool Local(HttpContext http){
            var ip=http.Connection.RemoteIpAddress;
            return ip is not null && (IPAddress.IsLoopback(ip) ||
                ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4()));
        }
        static string? Token(HttpContext http){
            var h=http.Request.Headers.Authorization.ToString();
            return h.Length<=4096 && h.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase)
                ?h[7..].Trim():null;
        }
        routes.MapGet("/api/dev/subscriptions/customer-offers/me",async(
            HttpContext http,CustomerCheckoutService service,CancellationToken ct)=>{
            if(!Local(http))return Results.NotFound();
            if(Token(http) is not { } token)return Results.Unauthorized();
            try{http.Response.Headers.CacheControl="no-store";
                return Results.Json(await service.OffersAsync(token,ct));}
            catch(UnauthorizedAccessException){return Results.StatusCode(403);}
            catch(InvalidOperationException ex){return Results.Conflict(new {message=ex.Message});}
        }).ExcludeFromDescription().RequireRateLimiting("signing-import");
        routes.MapPost("/api/dev/subscriptions/customer-checkout/cash",async(
            HttpContext http,[FromBody] CashRequest input,CustomerCheckoutService service,
            CancellationToken ct)=>{
            if(!Local(http))return Results.NotFound();
            if(http.Request.Headers.ContainsKey("Origin"))return Results.StatusCode(403);
            if(http.Request.ContentLength is >1024)return Results.StatusCode(413);
            if(Token(http) is not { } token)return Results.Unauthorized();
            try{return Results.Json(await service.RequestCashAsync(token,input.PlanId,
                input.WantsOfficialInvoice,0,input.OperationKey,ct),statusCode:201);}
            catch(UnauthorizedAccessException){return Results.StatusCode(403);}
            catch(ArgumentException){return Results.BadRequest();}
            catch(InvalidOperationException ex){return Results.Conflict(new {message=ex.Message});}
        }).ExcludeFromDescription().RequireRateLimiting("signing-import");
        return routes;
    }
}
