using System.Net;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
namespace ArginTaxPayer.Api.Subscriptions;

public static class Phase12SmsTopupEndpoints
{
    public sealed record StartRequest(long AmountIrr,Guid? CustomerUserId);
    public sealed record SepSettingsRequest(int TerminalNumber,Guid AssignedAdminUserId);
    public static IEndpointRouteBuilder MapPhase12SmsTopupEndpoints(this IEndpointRouteBuilder routes)
    {
        static bool Local(HttpContext h){var ip=h.Connection.RemoteIpAddress;return ip is not null&&(IPAddress.IsLoopback(ip)||ip.IsIPv4MappedToIPv6&&IPAddress.IsLoopback(ip.MapToIPv4()));}
        static string? Token(HttpContext h){var v=h.Request.Headers.Authorization.ToString();return v.Length<=4096&&v.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase)?v[7..].Trim():null;}
        routes.MapGet("/api/dev/subscriptions/sms-sep-settings",async(HttpContext h,SmsSepSettingsService svc,CancellationToken ct)=>{
            if(!Local(h))return Results.NotFound();if(Token(h) is not { } t)return Results.Unauthorized();
            try{return Results.Json(await svc.GetAsync(t,ct));}catch(UnauthorizedAccessException){return Results.StatusCode(403);}
        }).ExcludeFromDescription().RequireRateLimiting("signing-import");
        routes.MapGet("/api/dev/subscriptions/sms-sep-admins",async(HttpContext h,SmsSepSettingsService svc,CancellationToken ct)=>{
            if(!Local(h))return Results.NotFound();if(Token(h) is not { } t)return Results.Unauthorized();
            try{return Results.Json(await svc.ListAssignableAdminsAsync(t,ct));}catch(UnauthorizedAccessException){return Results.StatusCode(403);}
        }).ExcludeFromDescription().RequireRateLimiting("signing-import");
        routes.MapPut("/api/dev/subscriptions/sms-sep-settings",async(HttpContext h,SepSettingsRequest body,SmsSepSettingsService svc,CancellationToken ct)=>{
            if(!Local(h))return Results.NotFound();if(h.Request.Headers.ContainsKey("Origin"))return Results.StatusCode(403);if(h.Request.ContentLength is >2048)return Results.StatusCode(413);if(Token(h) is not { } t)return Results.Unauthorized();
            try{return Results.Json(await svc.SaveAsync(t,body.TerminalNumber,body.AssignedAdminUserId,ct));}catch(UnauthorizedAccessException){return Results.StatusCode(403);}catch(ArgumentException){return Results.BadRequest();}
        }).ExcludeFromDescription().RequireRateLimiting("signing-import");
        routes.MapPost("/api/dev/subscriptions/sms-topups/start",async(HttpContext h,StartRequest body,SmsSepTopupService svc,CancellationToken ct)=>{
            if(!Local(h))return Results.NotFound();if(h.Request.Headers.ContainsKey("Origin"))return Results.StatusCode(403);if(h.Request.ContentLength is >2048)return Results.StatusCode(413);if(Token(h) is not { } t)return Results.Unauthorized();h.Response.Headers.CacheControl="no-store";
            try{return Results.Json(await svc.StartAsync(t,body.AmountIrr,body.CustomerUserId,ct));}catch(UnauthorizedAccessException){return Results.StatusCode(403);}catch(ArgumentOutOfRangeException){return Results.BadRequest(new{error="invalid_amount"});}catch(ArgumentException){return Results.BadRequest();}catch(InvalidOperationException){return Results.Conflict(new{error="sep_unavailable"});}
        }).ExcludeFromDescription().RequireRateLimiting("signing-import");
        routes.MapGet("/api/dev/subscriptions/sms-topups/{topupId:guid}",async(HttpContext h,Guid topupId,SmsSepTopupService svc,CancellationToken ct)=>{
            if(!Local(h))return Results.NotFound();if(Token(h) is not { } t)return Results.Unauthorized();h.Response.Headers.CacheControl="no-store";
            try{return Results.Json(await svc.ReadAsync(t,topupId,ct));}catch(UnauthorizedAccessException){return Results.StatusCode(403);}
        }).ExcludeFromDescription().RequireRateLimiting("signing-import");
        routes.MapPost("/api/dev/subscriptions/sms-topups/{topupId:guid}/reconcile",async(HttpContext h,Guid topupId,SmsSepTopupService svc,CancellationToken ct)=>{
            if(!Local(h))return Results.NotFound();if(h.Request.Headers.ContainsKey("Origin"))return Results.StatusCode(403);if(Token(h) is not { } t)return Results.Unauthorized();
            try{return Results.Json(new{outcome=await svc.ReconcileAsync(t,topupId,ct)});}catch(UnauthorizedAccessException){return Results.StatusCode(403);}catch(ArgumentException){return Results.NotFound();}
        }).ExcludeFromDescription().RequireRateLimiting("signing-import");

        return routes;
    }

    public static IEndpointRouteBuilder MapSepSmsCallbackEndpoint(this IEndpointRouteBuilder routes)
    {
        // Public SEP browser return: no browser callback field is trusted as payment evidence.
        routes.MapPost("/api/payments/sep/sms/callback",async(HttpContext h,SmsSepTopupService svc,CancellationToken ct)=>{
            h.Response.Headers.CacheControl="no-store";
            if(h.Request.ContentLength is null or >8192)return Results.StatusCode(413);
            if(!h.Request.HasFormContentType)return Results.BadRequest();
            IFormCollection form;try{form=await h.Request.ReadFormAsync(ct);}catch{return Results.BadRequest();}
            static string? V(IFormCollection f,string k)=>f.TryGetValue(k,out var v)?v.ToString():null;
            var res=V(form,"ResNum");var id=await svc.FindTopupIdForCallbackAsync(res,ct);
            if(id is null)return Results.NotFound();
            var terminal=V(form,"TerminalId")??V(form,"MID");
            var callback=new SepCallback(res,V(form,"RefNum"),V(form,"State"),V(form,"TraceNo"),terminal,V(form,"Amount"));
            SepCallbackOutcome outcome;
            try{outcome=await svc.ProcessCallbackAsync(callback,ct);}catch{await svc.ReleaseCallbackToUnknownAsync(res,CancellationToken.None);outcome=SepCallbackOutcome.Pending;}
            return Results.Redirect(svc.BuildReturnUrl(id,outcome),permanent:false,preserveMethod:false);
        }).ExcludeFromDescription().RequireRateLimiting("sep-payment-callback");
        return routes;
    }
}
