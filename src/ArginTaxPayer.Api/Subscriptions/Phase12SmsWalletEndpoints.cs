using System.Net;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using Microsoft.AspNetCore.Mvc;
namespace ArginTaxPayer.Api.Subscriptions;
public static class Phase12SmsWalletEndpoints
{
    public sealed record GrantRequest(Guid CustomerUserId,long Units,string OperationKey,string Evidence);
    public static IEndpointRouteBuilder MapPhase12SmsWalletEndpoints(this IEndpointRouteBuilder routes)
    {
        static bool Local(HttpContext h) {
            var ip=h.Connection.RemoteIpAddress;
            return ip is not null && (IPAddress.IsLoopback(ip) ||
                ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4()));
        }
        static string? Token(HttpContext h) {
            var v=h.Request.Headers.Authorization.ToString();
            return v.Length<=4096&&v.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase)?v[7..].Trim():null;
        }
        routes.MapGet("/api/dev/subscriptions/sms-wallet/{customerId:guid}",async(
            HttpContext http,Guid customerId,SmsWalletService service,CancellationToken ct)=>
        {
            if(!Local(http))return Results.NotFound();
            http.Response.Headers.CacheControl="no-store";
            var token=Token(http);
            if(string.IsNullOrWhiteSpace(token))return Results.Unauthorized();
            try {
                var balance=await service.ReadAsync(token,customerId,ct);
                return Results.Json(new{balance.Granted,balance.Spent,balance.Available});
            } catch(UnauthorizedAccessException){return Results.StatusCode(403);}
        }).ExcludeFromDescription().RequireRateLimiting("signing-import");
        routes.MapPost("/api/dev/subscriptions/sms-wallet/grant",async(
            HttpContext http,[FromBody] GrantRequest request,SmsWalletService service,CancellationToken ct)=>
        {
            if(!Local(http))return Results.NotFound();
            if(http.Request.Headers.ContainsKey("Origin"))return Results.StatusCode(403);
            if(http.Request.ContentLength is > 2048)return Results.StatusCode(413);
            var token=Token(http);
            if(string.IsNullOrWhiteSpace(token))return Results.Unauthorized();
            try {
                var balance=await service.GrantAsync(token,request.CustomerUserId,
                    request.Units,request.OperationKey,request.Evidence,ct);
                return Results.Json(new{balance.Granted,balance.Spent,balance.Available});
            } catch(UnauthorizedAccessException){return Results.StatusCode(403);}
              catch(ArgumentException){return Results.BadRequest();}
              catch(InvalidOperationException){return Results.Conflict();}
        }).ExcludeFromDescription().RequireRateLimiting("signing-import");
        return routes;
    }
}
