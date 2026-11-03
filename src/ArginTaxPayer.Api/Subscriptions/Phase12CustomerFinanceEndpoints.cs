using System.Net;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
namespace ArginTaxPayer.Api.Subscriptions;
public static class Phase12CustomerFinanceEndpoints
{
    public static IEndpointRouteBuilder MapPhase12CustomerFinanceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/dev/subscriptions/customer-finance/me",async(
            HttpContext http,CustomerFinancialHistoryService svc,CancellationToken ct)=>
        {
            var ip=http.Connection.RemoteIpAddress;
            if(ip is null || !(IPAddress.IsLoopback(ip) ||
                ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4())))
                return Results.NotFound();
            var auth=http.Request.Headers.Authorization.ToString();
            var bearer=auth.Length<=4096 &&
                auth.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase)
                ?auth[7..].Trim():null;
            if(string.IsNullOrWhiteSpace(bearer))return Results.Unauthorized();
            try {
                http.Response.Headers.CacheControl="no-store";
                return Results.Json(await svc.ReadMineAsync(bearer,ct));
            } catch(UnauthorizedAccessException){return Results.StatusCode(403);}
        }).ExcludeFromDescription().RequireRateLimiting("signing-import");
        return app;
    }
}
