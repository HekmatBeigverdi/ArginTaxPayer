using System.Net;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
namespace ArginTaxPayer.Api.Subscriptions;
public static class Phase12PendingCashEndpoints
{
 public static IEndpointRouteBuilder MapPhase12PendingCashEndpoints(this IEndpointRouteBuilder routes)
 {
  routes.MapGet("/api/dev/subscriptions/cash-pending",async(
    HttpContext ctx,CommercialPendingCashService service,CancellationToken ct)=>{
   var ip=ctx.Connection.RemoteIpAddress;
   if(ip is null||!(IPAddress.IsLoopback(ip) ||
       (ip.IsIPv4MappedToIPv6&&IPAddress.IsLoopback(ip.MapToIPv4()))))
    return Results.NotFound();
   var auth=ctx.Request.Headers.Authorization.ToString();
   var token=auth.Length<=4096&&auth.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase)
    ?auth[7..].Trim():null;
   if(string.IsNullOrWhiteSpace(token))return Results.Unauthorized();
   try {ctx.Response.Headers.CacheControl="no-store";
    return Results.Json(await service.ReadMineAsync(token,ct));}
   catch(UnauthorizedAccessException){return Results.StatusCode(403);}
  }).ExcludeFromDescription().RequireRateLimiting("signing-import");
  return routes;
 }
}
