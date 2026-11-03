using System.Net;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
namespace ArginTaxPayer.Api.Subscriptions;

public static class Phase12SmsEndpoints
{
    public sealed record SettingsRequest(int ServiceFeeBasisPoints,long ReservationCeilingIrr,bool? CustomerBillingEnabled);
    public static IEndpointRouteBuilder MapPhase12SmsEndpoints(this IEndpointRouteBuilder routes)
    {
        static bool Local(HttpContext h) { var ip=h.Connection.RemoteIpAddress; return ip is not null && (IPAddress.IsLoopback(ip) || ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4())); }
        static string? Token(HttpContext h) { var v=h.Request.Headers.Authorization.ToString(); return v.Length<=4096&&v.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase)?v[7..].Trim():null; }
        routes.MapGet("/api/dev/subscriptions/sms-credit",async(HttpContext h,SmsCreditService svc,CancellationToken ct)=>{
            if(!Local(h))return Results.NotFound(); if(Token(h) is not { } t)return Results.Unauthorized(); h.Response.Headers.CacheControl="no-store";
            try{return Results.Json(await svc.ReadMineAsync(t,ct));}catch(UnauthorizedAccessException){return Results.StatusCode(403);}
        }).ExcludeFromDescription().RequireRateLimiting("signing-import");
        routes.MapGet("/api/dev/subscriptions/sms-usage",async(HttpContext h,SmsUsageReadService svc,int? page,Guid? partnerUserId,CancellationToken ct)=>{
            if(!Local(h))return Results.NotFound(); if(Token(h) is not { } t)return Results.Unauthorized(); h.Response.Headers.CacheControl="no-store";
            try{return Results.Json(await svc.ReadAsync(t,partnerUserId,Math.Max(1,page??1),ct));}catch(UnauthorizedAccessException){return Results.StatusCode(403);}
        }).ExcludeFromDescription().RequireRateLimiting("signing-import");
        routes.MapGet("/api/dev/subscriptions/sms-settings",async(HttpContext h,SmsServiceSettingsService svc,CancellationToken ct)=>{
            if(!Local(h))return Results.NotFound(); if(Token(h) is not { } t)return Results.Unauthorized();
            try{return Results.Json(await svc.GetAsync(t,ct));}catch(UnauthorizedAccessException){return Results.StatusCode(403);}
        }).ExcludeFromDescription().RequireRateLimiting("signing-import");
        routes.MapGet("/api/dev/subscriptions/sms-partners",async(HttpContext h,CustomerCommercialOwnershipService svc,CancellationToken ct)=>{
            if(!Local(h))return Results.NotFound(); if(Token(h) is not { } t)return Results.Unauthorized();
            var result=await svc.ListPartnersAsync(t,ct);return result.Decision switch {
                ArginTaxPayer.Infrastructure.Persistence.Taxpayers.CustomerOwnershipDecision.Success=>Results.Json(result.Partners),
                ArginTaxPayer.Infrastructure.Persistence.Taxpayers.CustomerOwnershipDecision.Unauthenticated=>Results.Unauthorized(),_=>Results.StatusCode(403)};
        }).ExcludeFromDescription().RequireRateLimiting("signing-import");
        routes.MapPut("/api/dev/subscriptions/sms-settings",async(HttpContext h,SettingsRequest b,SmsServiceSettingsService svc,CancellationToken ct)=>{
            if(!Local(h))return Results.NotFound(); if(h.Request.Headers.ContainsKey("Origin"))return Results.StatusCode(403); if(Token(h) is not { } t)return Results.Unauthorized();
            try{return Results.Json(await svc.SaveAsync(t,b.ServiceFeeBasisPoints,b.ReservationCeilingIrr,b.CustomerBillingEnabled??true,ct));}catch(UnauthorizedAccessException){return Results.StatusCode(403);}catch(ArgumentException){return Results.BadRequest();}
        }).ExcludeFromDescription().RequireRateLimiting("signing-import");
        return routes;
    }
}
