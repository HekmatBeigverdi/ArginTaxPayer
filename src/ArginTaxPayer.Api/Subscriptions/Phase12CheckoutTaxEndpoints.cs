using System.Net;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using Microsoft.AspNetCore.Mvc;
namespace ArginTaxPayer.Api.Subscriptions;
public static class Phase12CheckoutTaxEndpoints
{
 public sealed record ChangeRate(int VatBasisPoints);
 public static IEndpointRouteBuilder MapPhase12CheckoutTaxEndpoints(this IEndpointRouteBuilder app)
 {
  static string? Token(HttpContext ctx) {
   var h=ctx.Request.Headers.Authorization.ToString();
   return h.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase)&&h.Length<=4096?h[7..].Trim():null;
  }
  static bool Local(HttpContext ctx) {
   var ip=ctx.Connection.RemoteIpAddress;
   return ip is not null&&(IPAddress.IsLoopback(ip) ||
     ip.IsIPv4MappedToIPv6&&IPAddress.IsLoopback(ip.MapToIPv4()));
  }
  app.MapGet("/api/dev/subscriptions/checkout-tax",async(HttpContext ctx,
    CommercialCheckoutTaxSettingsService svc,CancellationToken ct)=>{
   if(!Local(ctx))return Results.NotFound();
   if(Token(ctx) is not { } token)return Results.Unauthorized();
   try {ctx.Response.Headers.CacheControl="no-store";
    return Results.Json(new{vatBasisPoints=await svc.ReadAsync(token,ct)});}
   catch(UnauthorizedAccessException){return Results.StatusCode(403);}
  }).ExcludeFromDescription().RequireRateLimiting("signing-import");
  app.MapPut("/api/dev/subscriptions/checkout-tax",async(HttpContext ctx,
    [FromBody] ChangeRate body,CommercialCheckoutTaxSettingsService svc,CancellationToken ct)=>{
   if(!Local(ctx))return Results.NotFound();
   if(ctx.Request.Headers.ContainsKey("Origin"))return Results.StatusCode(403);
   if(ctx.Request.ContentLength is >512)return Results.StatusCode(413);
   if(Token(ctx) is not { } token)return Results.Unauthorized();
   try {await svc.SaveAsync(token,body.VatBasisPoints,ct);return Results.NoContent();}
   catch(UnauthorizedAccessException){return Results.StatusCode(403);}
   catch(ArgumentException){return Results.BadRequest();}
  }).ExcludeFromDescription().RequireRateLimiting("signing-import");
  return app;
 }
}
