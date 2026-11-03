using System.Net;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using Microsoft.AspNetCore.Mvc;
namespace ArginTaxPayer.Api.Subscriptions;
public static class Phase12ActivationEndpoints
{
    public sealed record ApproveOrderRequest(Guid OrderId);
    public static IEndpointRouteBuilder MapPhase12ActivationEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/dev/subscriptions/orders/approve", HandleApprovalAsync)
            .ExcludeFromDescription().RequireRateLimiting("signing-import");
        // Keep the earlier route for clients that have not moved to the approval name yet.
        routes.MapPost("/api/dev/subscriptions/paid-orders/activate", HandleApprovalAsync)
            .ExcludeFromDescription().RequireRateLimiting("signing-import");
        routes.MapPost("/api/dev/subscriptions/orders/reject", HandleRejectionAsync)
            .ExcludeFromDescription().RequireRateLimiting("signing-import");
        return routes;
    }

    private static async Task<IResult> HandleApprovalAsync(HttpContext http,
        [FromBody] ApproveOrderRequest input, CommercialSubscriptionActivationService service,
        CancellationToken ct)
    {
        var ip=http.Connection.RemoteIpAddress;
        if(ip is null || !(IPAddress.IsLoopback(ip) ||
           ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4())))
            return Results.NotFound();
        if(http.Request.Headers.ContainsKey("Origin"))return Results.StatusCode(403);
        if(http.Request.ContentLength is > 1024)return Results.StatusCode(413);
        var authorization=http.Request.Headers.Authorization.ToString();
        var bearer=authorization.Length<=4096 &&
            authorization.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase)
            ?authorization[7..].Trim():null;
        if(string.IsNullOrWhiteSpace(bearer))return Results.Unauthorized();
        try
        {
            var subscription=await service.ApproveOrderAsync(bearer,input.OrderId,
                DateTime.UtcNow,ct);
            return Results.Json(new {subscriptionId=subscription});
        }
        catch(UnauthorizedAccessException){return Results.StatusCode(403);}
        catch(ArgumentException){return Results.BadRequest();}
        catch(InvalidOperationException ex){return Results.Conflict(new {message=ex.Message});}
    }

    private static async Task<IResult> HandleRejectionAsync(HttpContext http,
        [FromBody] ApproveOrderRequest input, CommercialSubscriptionActivationService service,
        CancellationToken ct)
    {
        var ip=http.Connection.RemoteIpAddress;
        if(ip is null || !(IPAddress.IsLoopback(ip) ||
           ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4())))
            return Results.NotFound();
        if(http.Request.Headers.ContainsKey("Origin"))return Results.StatusCode(403);
        if(http.Request.ContentLength is > 1024)return Results.StatusCode(413);
        var authorization=http.Request.Headers.Authorization.ToString();
        var bearer=authorization.Length<=4096 &&
            authorization.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase)
            ?authorization[7..].Trim():null;
        if(string.IsNullOrWhiteSpace(bearer))return Results.Unauthorized();
        try
        {
            await service.RejectOrderAsync(bearer,input.OrderId,DateTime.UtcNow,ct);
            return Results.Json(new {rejected=true});
        }
        catch(UnauthorizedAccessException){return Results.StatusCode(403);}
        catch(ArgumentException){return Results.BadRequest();}
        catch(InvalidOperationException ex){return Results.Conflict(new {message=ex.Message});}
    }
}
