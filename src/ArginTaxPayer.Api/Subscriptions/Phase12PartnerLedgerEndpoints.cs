using System.Net;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

namespace ArginTaxPayer.Api.Subscriptions;

public static class Phase12PartnerLedgerEndpoints
{
    public static IEndpointRouteBuilder MapPhase12PartnerLedgerEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/dev/subscriptions/revenue-ledger",async (
            HttpContext http,PartnerRevenueLedgerReadService service,int? page,int? pageSize,
            CancellationToken ct) =>
        {
            var ip=http.Connection.RemoteIpAddress;
            if(ip is null || !(IPAddress.IsLoopback(ip) ||
                (ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4()))))
                return Results.NotFound();
            http.Response.Headers.CacheControl="no-store";
            var header=http.Request.Headers.Authorization.ToString();
            var bearer=header.Length<=4096 &&
                header.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase)
                ?header[7..].Trim():null;
            if(string.IsNullOrWhiteSpace(bearer))return Results.Unauthorized();
            try
            {
                var result=await service.ReadAsync(bearer,page??1,pageSize??15,ct);
                return Results.Json(result);
            }
            catch(UnauthorizedAccessException){return Results.StatusCode(403);}
            catch(ArgumentOutOfRangeException){return Results.BadRequest();}
        }).RequireRateLimiting("signing-import").ExcludeFromDescription();
        return routes;
    }
}
