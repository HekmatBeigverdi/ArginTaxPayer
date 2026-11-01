using System.Net;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
namespace ArginTaxPayer.Api.Subscriptions;
public static class Phase12CustomerOverviewEndpoints
{
    public static IEndpointRouteBuilder MapPhase12CustomerOverviewEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/dev/subscriptions/customer-overview/me",async(
            HttpContext http,CustomerSubscriptionOverviewService service,CancellationToken ct)=>
        {
            var ip=http.Connection.RemoteIpAddress;
            if(ip is null || !(IPAddress.IsLoopback(ip) ||
               (ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4()))))
                return Results.NotFound();
            var h=http.Request.Headers.Authorization.ToString();
            var bearer=h.Length<=4096 && h.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase)
                ?h[7..].Trim():null;
            if(string.IsNullOrWhiteSpace(bearer))return Results.Unauthorized();
            try {
                http.Response.Headers.CacheControl="no-store";
                return Results.Json(await service.ReadMineAsync(bearer,ct));
            }catch(UnauthorizedAccessException){return Results.StatusCode(403);}
        }).ExcludeFromDescription().RequireRateLimiting("signing-import");
        return app;
    }
}
