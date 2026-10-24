using System.Net;
using ArginTaxPayer.Infrastructure.Persistence.Signing;
using Microsoft.AspNetCore.Mvc;

namespace ArginTaxPayer.Api.Taxpayers;

/// <summary>
/// Scoped development-only Sandbox registration. NEVER uses the Production
/// authority HTTP client, private-key vault, or sends a request to tax.gov.ir.
/// </summary>
public static class SandboxFiscalMemoryDevelopmentEndpoints
{
    public static IEndpointRouteBuilder MapSandboxFiscalMemoryDevelopmentEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group=routes.MapGroup("/api/dev/taxpayers/{taxpayerId:guid}/sandbox-memory")
            .ExcludeFromDescription().RequireRateLimiting("signing-import");
        group.MapGet("",async(Guid taxpayerId,HttpContext http,
            SandboxFiscalMemoryMetadataService service,CancellationToken ct)=>
        {
            if(!Local(http))return Results.NotFound();
            var result=await service.GetAsync(Bearer(http),taxpayerId,ct);
            return Reply(result);
        });
        group.MapPost("",async(Guid taxpayerId,[FromBody] SandboxMemoryRegistration request,
            HttpContext http,SandboxFiscalMemoryMetadataService service,CancellationToken ct)=>
        {
            if(!Local(http))return Results.NotFound();
            if(http.Request.Headers.ContainsKey("Origin"))return Results.StatusCode(403);
            if(http.Request.ContentLength is > 12000)return Results.StatusCode(413);
            var result=await service.RegisterAsync(Bearer(http),taxpayerId,request,ct);
            return Reply(result);
        });
        group.MapPost("/connection-test",async(Guid taxpayerId,HttpContext http,
            SandboxFiscalMemoryMetadataService service,CancellationToken ct)=>
        {
            if(!Local(http))return Results.NotFound();
            if(http.Request.Headers.ContainsKey("Origin"))return Results.StatusCode(403);
            var scope=await service.GetAsync(Bearer(http),taxpayerId,ct);
            if(scope.StatusCode!=200)return Reply(scope);
            // Fail closed: official Sandbox API and separate encrypted private
            // custody must be installed before any signed token request.
            return Results.Json(new {status="Unavailable",
                error="sandbox_authority_api_not_verified",authenticationConfirmed=false,
                environment="Sandbox"},statusCode:503);
        });
        return routes;
    }

    private static IResult Reply(SandboxMemoryResult result)=>
        Results.Json(new {error=result.Error,environment="Sandbox",
            data=result.Value,connectionTestReady=false},
            statusCode:result.StatusCode);

    private static bool Local(HttpContext http)
    {
        var ip=http.Connection.RemoteIpAddress;
        return ip is not null && (IPAddress.IsLoopback(ip)||
            ip.IsIPv4MappedToIPv6&&IPAddress.IsLoopback(ip.MapToIPv4()));
    }

    private static string? Bearer(HttpContext http)
    {
        var authorization=http.Request.Headers.Authorization.ToString();
        return authorization.Length<=4096 &&
            authorization.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase)
            ? authorization[7..].Trim():null;
    }
}
