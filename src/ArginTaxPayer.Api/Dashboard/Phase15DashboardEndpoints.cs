using System.Net;
using ArginTaxPayer.Api.Dashboard;
using Microsoft.AspNetCore.Mvc;

namespace ArginTaxPayer.Api.Dashboard;

/// <summary>Read-only, authenticated, localhost-only dashboard queries.</summary>
public static class Phase15DashboardEndpoints
{
    public static IEndpointRouteBuilder MapPhase15DashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group=app.MapGroup("/api/dev/dashboard").ExcludeFromDescription()
            .RequireRateLimiting("signing-import");
        group.MapGet("/{section}",async (
            string section,HttpContext http,Phase15DashboardReadService service,
            [FromQuery] string? period,[FromQuery] string? from,[FromQuery] string? to,
            [FromQuery] Guid? taxpayerId,[FromQuery] Guid? partnerUserId,CancellationToken ct) =>
        {
            var ip=http.Connection.RemoteIpAddress;
            if(ip is null || !(IPAddress.IsLoopback(ip) ||
                 ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4())))
                return Results.NotFound();
            http.Response.Headers.CacheControl="private, no-store";
            var header=http.Request.Headers.Authorization.ToString();
            if(header.Length>4096 || !header.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(header[7..]))return Results.Unauthorized();
            if(section is not ("overview" or "trends" or "finance" or "attention" or "recent" or "partner" or "root" or "financial-trends" or "subscriptions-watch"))
                return Results.NotFound();
            if((section=="finance" || section=="partner" || section=="financial-trends") && taxpayerId.HasValue ||
               section!="finance" && partnerUserId.HasValue ||
               section=="partner" && (partnerUserId.HasValue || period is not null || from is not null || to is not null) ||
               section=="root" && (taxpayerId.HasValue || partnerUserId.HasValue) ||
               section=="financial-trends" && partnerUserId.HasValue ||
               section=="subscriptions-watch" && (taxpayerId.HasValue || partnerUserId.HasValue || period is not null || from is not null || to is not null))
                return Results.BadRequest(new {error="invalid_filter"});
            try
            {
                var window=Phase15DashboardReadService.ResolveWindow(period,from,to,DateTime.UtcNow);
                var bearer=header[7..].Trim();
                object value=section switch
                {
                    "overview"=>await service.OverviewAsync(bearer,window,taxpayerId,ct),
                    "trends"=>await service.TrendsAsync(bearer,window,taxpayerId,ct),
                    "finance"=>await service.FinanceAsync(bearer,window,partnerUserId,ct),
                    "financial-trends"=>await service.FinancialTrendsAsync(bearer,window,ct),
                    "subscriptions-watch"=>await service.SubscriptionWatchAsync(bearer,ct),
                    "partner"=>await service.PartnerOverviewAsync(bearer,ct),
                    "root"=>await service.RootOverviewAsync(bearer,window,ct),
                    "attention"=>await service.AttentionAsync(bearer,window,taxpayerId,ct),
                    _=>await service.RecentAsync(bearer,window,taxpayerId,ct)
                };
                return Results.Json(value);
            }
            catch(UnauthorizedAccessException){return Results.StatusCode(403);}
            catch(KeyNotFoundException){return Results.NotFound();}
            catch(ArgumentException){return Results.BadRequest(new {error="invalid_filter"});}
        });
        return app;
    }
}
