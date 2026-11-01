using System.Net;
using ArginTaxPayer.Domain.Subscriptions;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using Microsoft.AspNetCore.Mvc;

namespace ArginTaxPayer.Api.Subscriptions;

public static class Phase12PlanAdministrationEndpoints
{
    public sealed record CreatePlanRequest(string Code, SubscriptionPlanKind Kind,
        int ValidityDays, long? InvoiceUnitLimit);

    public static IEndpointRouteBuilder MapPhase12PlanAdministrationEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/dev/subscriptions/plans", async (
            HttpContext http, SubscriptionPlanAdministrationService service, CancellationToken ct) =>
        {
            if(!IsLocal(http))return Results.NotFound();
            var token=AccessToken(http);
            if(token is null)return Results.Unauthorized();
            try {http.Response.Headers.CacheControl="no-store";
                return Results.Json(await service.ListAsync(token,ct));}
            catch(UnauthorizedAccessException){return Results.StatusCode(403);}
        }).ExcludeFromDescription().RequireRateLimiting("signing-import");
        routes.MapPut("/api/dev/subscriptions/plans/{id:guid}", async (
            HttpContext http,Guid id,[FromBody] CreatePlanRequest input,
            SubscriptionPlanAdministrationService service,CancellationToken ct) =>
        {
            if(!IsLocal(http)||http.Request.Headers.ContainsKey("Origin"))return Results.StatusCode(403);
            var token=AccessToken(http);
            if(token is null)return Results.Unauthorized();
            try{
                var plan=await service.UpdateAsync(token,id,input.Code,input.Kind,
                    input.ValidityDays,input.InvoiceUnitLimit,ct);
                return Results.Json(new {plan.Id,plan.Code,plan.Kind,plan.ValidityDays,plan.InvoiceUnitLimit,plan.IsActive});
            }catch(UnauthorizedAccessException){return Results.StatusCode(403);}
             catch(ArgumentException){return Results.BadRequest();}
             catch(InvalidOperationException){return Results.Conflict();}
        }).ExcludeFromDescription().RequireRateLimiting("signing-import");
        routes.MapDelete("/api/dev/subscriptions/plans/{id:guid}", async (
            HttpContext http,Guid id,SubscriptionPlanAdministrationService service,CancellationToken ct) =>
        {
            if(!IsLocal(http)||http.Request.Headers.ContainsKey("Origin"))return Results.StatusCode(403);
            var token=AccessToken(http);
            if(token is null)return Results.Unauthorized();
            try{await service.DeactivateAsync(token,id,ct);return Results.NoContent();}
            catch(UnauthorizedAccessException){return Results.StatusCode(403);}
            catch(InvalidOperationException){return Results.Conflict();}
        }).ExcludeFromDescription().RequireRateLimiting("signing-import");
        routes.MapPost("/api/dev/subscriptions/plans", async (
            HttpContext http, [FromBody] CreatePlanRequest input,
            SubscriptionPlanAdministrationService service, CancellationToken ct) =>
        {
            var ip = http.Connection.RemoteIpAddress;
            if (ip is null || !(IPAddress.IsLoopback(ip) ||
                (ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4()))))
                return Results.NotFound();
            if (http.Request.Headers.ContainsKey("Origin")) return Results.StatusCode(403);
            if (http.Request.ContentLength is > 1024) return Results.StatusCode(413);
            var header = http.Request.Headers.Authorization.ToString();
            var bearer = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) &&
                         header.Length <= 4096 ? header[7..].Trim() : null;
            if (bearer is null) return Results.Unauthorized();
            try
            {
                var plan = await service.CreateAsync(bearer, input.Code, input.Kind,
                    input.ValidityDays, input.InvoiceUnitLimit, ct);
                return Results.Json(new {plan.Id, plan.Code, plan.Kind, plan.ValidityDays,
                    plan.InvoiceUnitLimit, plan.IsActive}, statusCode: 201);
            }
            catch (UnauthorizedAccessException) { return Results.StatusCode(403); }
            catch (ArgumentException) { return Results.BadRequest(); }
            catch (InvalidOperationException) { return Results.Conflict(); }
        }).ExcludeFromDescription().RequireRateLimiting("signing-import");
        return routes;
    }
    private static bool IsLocal(HttpContext http)
    {
        var ip=http.Connection.RemoteIpAddress;
        return ip is not null && (IPAddress.IsLoopback(ip) ||
            ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4()));
    }
    private static string? AccessToken(HttpContext http)
    {
        var h=http.Request.Headers.Authorization.ToString();
        return h.Length<=4096 && h.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase)
            ?h[7..].Trim():null;
    }

}
