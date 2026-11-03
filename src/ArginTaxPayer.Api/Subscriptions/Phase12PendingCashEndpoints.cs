using System.Net;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
namespace ArginTaxPayer.Api.Subscriptions;
public static class Phase12PendingCashEndpoints
{
 public static IEndpointRouteBuilder MapPhase12PendingCashEndpoints(this IEndpointRouteBuilder routes)
 {
  routes.MapGet("/api/dev/subscriptions/cash-pending",async(
    HttpContext ctx,CommercialPendingCashService service,int? page,CancellationToken ct)=>{
   var ip=ctx.Connection.RemoteIpAddress;
   if(ip is null||!(IPAddress.IsLoopback(ip) ||
       (ip.IsIPv4MappedToIPv6&&IPAddress.IsLoopback(ip.MapToIPv4()))))
    return Results.NotFound();
   var auth=ctx.Request.Headers.Authorization.ToString();
   var token=auth.Length<=4096&&auth.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase)
    ?auth[7..].Trim():null;
   if(string.IsNullOrWhiteSpace(token))return Results.Unauthorized();
   try {ctx.Response.Headers.CacheControl="no-store";
    return Results.Json(await service.ReadMineAsync(token,page??1,ct));}
   catch(UnauthorizedAccessException){return Results.StatusCode(403);}
   catch(ArgumentException){return Results.BadRequest();}
  }).ExcludeFromDescription().RequireRateLimiting("signing-import");
  routes.MapGet("/api/dev/subscriptions/sales-report",async(
    HttpContext ctx,CommercialPendingCashService service,string? period,int? page,Guid? partnerUserId,
    CancellationToken ct)=>{
   var ip=ctx.Connection.RemoteIpAddress;
   if(ip is null||!(IPAddress.IsLoopback(ip) ||
       (ip.IsIPv4MappedToIPv6&&IPAddress.IsLoopback(ip.MapToIPv4()))))
    return Results.NotFound();
   var auth=ctx.Request.Headers.Authorization.ToString();
   var token=auth.Length<=4096&&auth.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase)
    ?auth[7..].Trim():null;
   if(string.IsNullOrWhiteSpace(token))return Results.Unauthorized();
   ctx.Response.Headers.CacheControl="no-store";
   try {return Results.Json(await service.ReadSalesAsync(token,period??"month",DateTime.UtcNow,
      page??1,ct,partnerUserId));}
   catch(UnauthorizedAccessException){return Results.StatusCode(403);}
   catch(ArgumentException){return Results.BadRequest();}
  }).ExcludeFromDescription().RequireRateLimiting("signing-import");
  routes.MapGet("/api/dev/subscriptions/collections/history",async(
    HttpContext ctx,CommercialPendingCashService service,Guid orderId,int? page,CancellationToken ct)=>{
   var ip=ctx.Connection.RemoteIpAddress;
   if(ip is null||!(IPAddress.IsLoopback(ip)||(ip.IsIPv4MappedToIPv6&&IPAddress.IsLoopback(ip.MapToIPv4()))))
    return Results.NotFound();
   var auth=ctx.Request.Headers.Authorization.ToString();
   var token=auth.Length<=4096&&auth.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase)?auth[7..].Trim():null;
   if(string.IsNullOrWhiteSpace(token))return Results.Unauthorized();
   ctx.Response.Headers.CacheControl="no-store";
   try{return Results.Json(await service.ReadCollectionsAsync(token,orderId,page??1,ct));}
   catch(UnauthorizedAccessException){return Results.StatusCode(403);}
   catch(ArgumentException){return Results.BadRequest();}
  }).ExcludeFromDescription().RequireRateLimiting("signing-import");
  return routes;
 }
}
