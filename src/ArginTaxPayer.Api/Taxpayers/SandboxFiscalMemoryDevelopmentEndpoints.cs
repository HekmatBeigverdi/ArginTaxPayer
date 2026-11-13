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
            var selected=SelectedMemoryId(http);
            if(http.Request.Query.ContainsKey("memoryId")&&selected is null)return Results.BadRequest(new{error="invalid_memory_id"});
            var result=await service.GetAsync(Bearer(http),taxpayerId,ct,selected);
            var memories=await service.ListAsync(Bearer(http),taxpayerId,ct);
            return Results.Json(new{error=result.Error,environment="Sandbox",data=result.Value,
                memories=memories.Memories.Select(x=>new{id=x.Id,memoryIdentifier=x.MemoryIdentifier,
                    publicFingerprintSha256=x.PublicFingerprintSha256,revision=x.Revision,
                    keyCustodyReady=x.KeyCustodyReady}),connectionTestReady=false},statusCode:result.StatusCode);
        });
        group.MapPost("",async(Guid taxpayerId,[FromBody] SandboxMemoryRegistration request,
            HttpContext http,SandboxFiscalMemoryMetadataService service,CancellationToken ct)=>
        {
            if(!Local(http))return Results.NotFound();
            if(http.Request.Headers.ContainsKey("Origin"))return Results.StatusCode(403);
            if(http.Request.ContentLength is > 12000)return Results.StatusCode(413);
            try{
                var result=await service.RegisterAsync(Bearer(http),taxpayerId,request,ct);
                return Reply(result);
            }catch(Npgsql.PostgresException ex)when(ex.SqlState==Npgsql.PostgresErrorCodes.UniqueViolation){
                var (error,statusCode)=ex.ConstraintName switch
                {
                    // This is the pre-multi-memory schema constraint. A current
                    // application against that schema must not tell the operator
                    // that a different Sandbox identifier already exists.
                    "uq_sandbox_owner_taxpayer" => ("sandbox_memory_schema_upgrade_required",503),
                    // The current schema intentionally allows multiple identifiers
                    // but prevents registering the same one twice for this taxpayer.
                    "ux_sandbox_owner_taxpayer_identifier" => ("sandbox_memory_identifier_already_registered",409),
                    _ => ("sandbox_memory_registration_conflict",409)
                };
                return Results.Json(new{error,environment="Sandbox"},statusCode:statusCode);
            }
        });
        group.MapPost("/private-key",async(Guid taxpayerId,
            [FromBody] SandboxPrivateKeyImportRequest request,HttpContext http,
            SandboxFiscalMemoryMetadataService service,CancellationToken ct)=>
        {
            if(!Local(http))return Results.NotFound();
            if(http.Request.Headers.ContainsKey("Origin"))return Results.StatusCode(403);
            if(http.Request.ContentLength is > 16000)return Results.StatusCode(413);
            var selected=SelectedMemoryId(http);
            if(http.Request.Query.ContainsKey("memoryId")&&selected is null)return Results.BadRequest(new{error="invalid_memory_id"});
            var result=await service.ImportPrivateAsync(Bearer(http),taxpayerId,request,ct,selectedMemoryId:selected);
            return Reply(result);
        });
        group.MapPost("/private-key/restore",async(Guid taxpayerId,
            [FromBody] SandboxPrivateKeyRestoreRequest request,HttpContext http,
            SandboxFiscalMemoryMetadataService service,CancellationToken ct)=>
        {
            http.Response.Headers.CacheControl="no-store";
            if(!Local(http))return Results.NotFound();
            if(http.Request.Headers.ContainsKey("Origin"))return Results.StatusCode(403);
            if(http.Request.ContentLength is > 16000)return Results.StatusCode(413);
            var result=await service.RestorePrivateAsync(Bearer(http),taxpayerId,request,ct);
            return Reply(result);
        });
        group.MapPost("/key-self-test",async(Guid taxpayerId,HttpContext http,
            SandboxFiscalMemoryMetadataService service,CancellationToken ct)=>
        {
            if(!Local(http))return Results.NotFound();
            if(http.Request.Headers.ContainsKey("Origin"))return Results.StatusCode(403);
            if(http.Request.ContentLength is > 0)return Results.StatusCode(413);
            var selected=SelectedMemoryId(http);
            if(http.Request.Query.ContainsKey("memoryId")&&selected is null)return Results.BadRequest(new{error="invalid_memory_id"});
            var result=await service.SelfTestAsync(Bearer(http),taxpayerId,ct,selected);
            return Results.Json(new {environment="Sandbox",
                status=result.StatusCode==200?"OfflineKeyVerified":"Unavailable",
                error=result.Error,authorityAuthenticated=false},statusCode:result.StatusCode);
        });
        group.MapPost("/connection-test",async(Guid taxpayerId,HttpContext http,
            SandboxFiscalMemoryMetadataService service,CancellationToken ct)=>
        {
            http.Response.Headers.CacheControl="no-store";
            if(!Local(http))return Results.NotFound();
            if(http.Request.Headers.ContainsKey("Origin"))return Results.StatusCode(403);
            if(http.Request.ContentLength is > 0)return Results.StatusCode(413);
            if(http.Request.ContentType is not null)return Results.StatusCode(415);
            try
            {
                var selected=SelectedMemoryId(http);
                if(http.Request.Query.ContainsKey("memoryId")&&selected is null)return Results.BadRequest(new{error="invalid_memory_id"});
                var result=await service.TestAuthorityAsync(Bearer(http),taxpayerId,ct,selected);
                if(result.Result is null)
                {
                    var error=result.Error=="sandbox_authority_api_not_verified"
                        ?"sandbox_authority_api_not_verified":result.Error;
                    return Results.Json(new{environment="Sandbox",status="Unavailable",
                        error,authenticationConfirmed=false},
                        statusCode:result.StatusCode);
                }
                return Results.Json(new{
                    environment="Sandbox",
                    status=result.Result.Status.ToString(),
                    connectionRecorded=result.ConnectionRecorded,
                    safeCode=result.Result.SafeCode,
                    testedAtUtc=result.Result.TestedAtUtc,
                    authenticationConfirmed=result.Result.AuthenticationConfirmed,
                    portalRegistrationConfirmed=false,
                    fiscalMemoryActivationConfirmed=false,
                    fiscalInformation=result.Result.FiscalInformation
                },statusCode:200);
            }
            catch(OperationCanceledException)when(!ct.IsCancellationRequested)
            {
                return Results.Json(new{environment="Sandbox",status="TimedOut",
                    error="sandbox_authority_timeout",authenticationConfirmed=false},statusCode:504);
            }
            catch(OperationCanceledException){throw;}
            catch(Exception){
                return Results.Json(new{environment="Sandbox",status="Unavailable",
                    error="sandbox_connection_unavailable",authenticationConfirmed=false},statusCode:503);
            }
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

    private static Guid? SelectedMemoryId(HttpContext http) =>
        Guid.TryParse(http.Request.Query["memoryId"],out var id)&&id!=Guid.Empty?id:null;
}
