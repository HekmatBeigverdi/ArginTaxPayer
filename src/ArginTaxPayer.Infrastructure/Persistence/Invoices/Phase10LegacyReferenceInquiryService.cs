using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ArginTaxPayer.Application.Invoices;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using ArginTaxPayer.Infrastructure.Signing.Connection;
using ArginTaxPayer.Infrastructure.Signing.Offline;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public sealed record Phase10ReferenceInquiryResult(InvoiceApiDecision Decision,
    string SafeCode, string? ReferenceNumber=null, LegacyAuthorityInquiryReport? AuthorityResult=null);

/// <summary>
/// Correlated reference inquiry for a receipted Sandbox operation.
/// Stores bounded authority errors; never resends the original packet.
/// </summary>
public sealed class Phase10LegacyReferenceInquiryService(
    ArginTaxPayerDbContext db, TaxpayerScopeService scope,
    IConfiguration config, SandboxLegacySigningKeyLoader keys,
    Phase10DispatchEvidenceRepository evidence)
{
    public async Task<Phase10ReferenceInquiryResult> ReadAsync(
        string? bearer,Guid taxpayerId,Guid invoiceId,CancellationToken ct)
    {
        var actor=await scope.ResolveAsync(bearer,ct);
        if(actor.Decision==TaxpayerScopeDecision.Unauthenticated)
            return new(InvoiceApiDecision.Unauthenticated,"unauthenticated");
        if(actor.Decision!=TaxpayerScopeDecision.Allowed)
            return new(InvoiceApiDecision.Forbidden,"forbidden");
        var owner=await scope.ScopedQuery(actor.ActorId,actor.Level1)
            .Where(t=>t.Id==taxpayerId).Select(t=>t.CustomerUserId).SingleOrDefaultAsync(ct);
        if(owner is null || !await db.InvoiceDocuments.AsNoTracking().AnyAsync(i=>
            i.Id==invoiceId&&i.CustomerUserId==owner.Value&&i.TaxpayerId==taxpayerId,ct))
            return new(InvoiceApiDecision.NotFound,"not_found");
        var stored=await new LegacyAuthorityReportRepository(db).ReadAsync(owner.Value,taxpayerId,invoiceId,ct);
        return new(InvoiceApiDecision.Success,"inquiry_report_loaded",stored?.ReferenceNumber,stored?.Report);
    }

    public async Task<Phase10ReferenceInquiryResult> InquireAsync(
        string? bearer,Guid taxpayerId,Guid invoiceId,CancellationToken ct)
    {
        var actor=await scope.ResolveAsync(bearer,ct);
        if(actor.Decision==TaxpayerScopeDecision.Unauthenticated)
            return new(InvoiceApiDecision.Unauthenticated,"unauthenticated");
        if(actor.Decision!=TaxpayerScopeDecision.Allowed)
            return new(InvoiceApiDecision.Forbidden,"forbidden");
        var owner=await scope.ScopedQuery(actor.ActorId,actor.Level1)
            .Where(t=>t.Id==taxpayerId).Select(t=>t.CustomerUserId)
            .SingleOrDefaultAsync(ct);
        if(owner is null || owner.Value==Guid.Empty ||
            !await db.InvoiceDocuments.AsNoTracking().AnyAsync(i=>
                i.Id==invoiceId&&i.CustomerUserId==owner.Value&&i.TaxpayerId==taxpayerId,ct))
            return new(InvoiceApiDecision.NotFound,"not_found");
        if(!LegacyInvoiceSubmissionHttpTransport.TryResolveSandboxBase(config,out var baseUrl)||
            baseUrl is null)
            return new(InvoiceApiDecision.Invalid,"sandbox_inquiry_disabled");

        var conn=db.Database.GetDbConnection();
        var opened=conn.State!=System.Data.ConnectionState.Open;
        if(opened)await conn.OpenAsync(ct);
        Guid operationId,memoryId,attemptId;
        long stateRevision;
        string referenceNumber;
        string memoryCode,uid,fiscalTaxId,packetType;
        try
        {
            await using var cmd=conn.CreateCommand();
            cmd.CommandText="""
SELECT d.id,d.fiscal_memory_id,m.memory_identifier,
       o.payload_json::jsonb->>'fiscalTaxId',
       d.state_revision,
       o.payload_json::jsonb->>'requestJson',
       receipt.authority_reference
FROM invoice_dispatch_operations d
JOIN outbox_messages o ON o.id=d.id
JOIN sandbox_fiscal_memories m ON m.id=d.fiscal_memory_id
JOIN invoice_dispatch_receipts receipt ON receipt.operation_id=d.id AND receipt.receipt_kind=1 AND receipt.authority_reference IS NOT NULL
 WHERE d.customer_user_id=@owner AND d.taxpayer_id=@taxpayer
   AND d.invoice_id=@invoice AND d.state=5
   AND m.customer_user_id=@owner AND m.taxpayer_id=@taxpayer
   AND o.event_type='invoice.sandbox.legacy.dispatch.requested'
   AND EXISTS(SELECT 1 FROM invoice_dispatch_attempts a WHERE a.operation_id=d.id)
 ORDER BY d.created_at_utc DESC LIMIT 1
""";
            foreach(var (name,value) in new (string,object)[]{
                ("owner",owner.Value),("taxpayer",taxpayerId),("invoice",invoiceId)})
            {var p=cmd.CreateParameter();p.ParameterName=name;p.Value=value;cmd.Parameters.Add(p);}
            await using var reader=await cmd.ExecuteReaderAsync(ct);
            if(!await reader.ReadAsync(ct))
                return new(InvoiceApiDecision.Invalid,"acknowledged_operation_not_found");
            operationId=reader.GetGuid(0);memoryId=reader.GetGuid(1);
            memoryCode=reader.GetString(2);fiscalTaxId=reader.GetString(3);
            stateRevision=reader.GetInt64(4);
            referenceNumber=reader.GetString(6);
            // Bind evidence to the prior send attempt, not this inquiry request.
            attemptId=Guid.Empty;
            var requestJson=reader.GetString(5);
            using var request=JsonDocument.Parse(requestJson,new JsonDocumentOptions{MaxDepth=24});
            var packet=request.RootElement.GetProperty("packets")[0];
            uid=packet.GetProperty("uid").GetString()??"";
            packetType=packet.GetProperty("packetType").GetString()??"";
        }
        catch(JsonException){return new(InvoiceApiDecision.Invalid,"stored_packet_invalid");}
        finally{if(opened)await conn.CloseAsync();}
        var connection=db.Database.GetDbConnection();
        var openedAttempt=connection.State!=System.Data.ConnectionState.Open;
        if(openedAttempt)await connection.OpenAsync(ct);
        try
        {
            await using var attemptQuery=connection.CreateCommand();
            attemptQuery.CommandText="""
SELECT id FROM invoice_dispatch_attempts
WHERE operation_id=@operation ORDER BY attempt_number DESC LIMIT 1
""";
            var parameter=attemptQuery.CreateParameter();
            parameter.ParameterName="operation";parameter.Value=operationId;
            attemptQuery.Parameters.Add(parameter);
            attemptId=(await attemptQuery.ExecuteScalarAsync(ct)) is Guid id ? id : Guid.Empty;
        }
        finally {if(openedAttempt)await connection.CloseAsync();}
        if(attemptId==Guid.Empty)
            return new(InvoiceApiDecision.Invalid,"inquiry_attempt_not_found");
        if(uid!=operationId.ToString("D")||packetType!="INVOICE.V01"||
           referenceNumber.Length is < 1 or > 128 ||
           memoryCode.Length!=6||fiscalTaxId.Length!=22)
            return new(InvoiceApiDecision.Invalid,"stored_packet_identity_mismatch");

        using var key=await keys.RecoverAsync(owner.Value,taxpayerId,memoryId,ct);
        if(key is null)return new(InvoiceApiDecision.Invalid,"sandbox_signing_key_unavailable");
        var tokenClient=new LegacyInvoiceAccessTokenClient(config);
        var token=await tokenClient.RequestAsync(memoryCode,key.PrivateKey,key.PublicSpkiDer,ct);
        if(!token.Authenticated||token.AccessToken is null)
            return new(InvoiceApiDecision.Invalid,"sandbox_inquiry_token_unavailable");

        var trace=Guid.NewGuid().ToString("D");
        var stamp=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        var inquiryPacket=new {
            Uid=Guid.NewGuid().ToString("D"),PacketType="INQUIRY_BY_REFERENCE_NUMBER",Retry=false,
            // Legacy InquiryByReferenceNumberDto uses singular ReferenceNumber for its list.
            Data=new { ReferenceNumber=new[]{referenceNumber} },
            EncryptionKeyId=(string?)null,SymmetricKey=(string?)null,
            Iv=(string?)null,FiscalId=memoryCode,DataSignature=(string?)null
        };
        var signature=new LegacyOfflineSigningEngine().SignObject(
            key.PrivateKey,key.PublicSpkiDer,inquiryPacket,LegacySigningPurpose.Transfer,
            new Dictionary<string,string>{
                ["requestTraceId"]=trace,["timestamp"]=stamp,
                ["Authorization"]="Bearer "+token.AccessToken
            }).SignatureBase64;
        using var http=new HttpClient(new HttpClientHandler{
            AllowAutoRedirect=false,UseCookies=false,
            AutomaticDecompression=DecompressionMethods.None
        }){Timeout=TimeSpan.FromSeconds(15)};
        using var message=new HttpRequestMessage(HttpMethod.Post,
            new Uri(baseUrl,"self-tsp/sync/INQUIRY_BY_REFERENCE_NUMBER")){
            Content=JsonContent.Create(new {
                Signature=signature,SignatureKeyId=(string?)null,
                Packet=new {
                    inquiryPacket.Uid,inquiryPacket.PacketType,inquiryPacket.Retry,
                    inquiryPacket.Data,inquiryPacket.EncryptionKeyId,
                    inquiryPacket.SymmetricKey,inquiryPacket.Iv,inquiryPacket.FiscalId,
                    inquiryPacket.DataSignature,SignatureKeyId=(string?)null
                }
            },options:new JsonSerializerOptions(JsonSerializerDefaults.Web))
        };
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        message.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token.AccessToken);
        message.Headers.TryAddWithoutValidation("requestTraceId",trace);
        message.Headers.TryAddWithoutValidation("timestamp",stamp);
        try
        {
            using var response=await http.SendAsync(message,HttpCompletionOption.ResponseHeadersRead,ct);
            if(!response.IsSuccessStatusCode)
            {
                // Only an allowlisted HTTP class is reported. Never disclose
                // provider bodies, token, signed packet or transport headers.
                var safe = response.StatusCode switch
                {
                    HttpStatusCode.NotFound => "inquiry_http_404",
                    HttpStatusCode.MethodNotAllowed => "inquiry_http_405",
                    HttpStatusCode.Unauthorized => "inquiry_http_401",
                    HttpStatusCode.Forbidden => "inquiry_http_403",
                    HttpStatusCode.BadRequest => "inquiry_http_400",
                    HttpStatusCode.TooManyRequests => "inquiry_http_429",
                    HttpStatusCode.BadGateway => "inquiry_http_502",
                    HttpStatusCode.ServiceUnavailable => "inquiry_http_503",
                    HttpStatusCode.GatewayTimeout => "inquiry_http_504",
                    _ => "inquiry_http_other"
                };
                return new(InvoiceApiDecision.Success,safe);
            }
            if(response.Content.Headers.ContentLength is > 262144)
                return new(InvoiceApiDecision.Success,"inquiry_response_too_large");
            var bytes=await response.Content.ReadAsByteArrayAsync(ct);
            if(bytes.Length>262144)return new(InvoiceApiDecision.Success,"inquiry_response_too_large");
            using var doc=JsonDocument.Parse(bytes,new JsonDocumentOptions{MaxDepth=16});
            var root=doc.RootElement;
            if(root.TryGetProperty("errors",out var errors)&&
                errors.ValueKind!=JsonValueKind.Null &&
                (errors.ValueKind!=JsonValueKind.Array||errors.GetArrayLength()>0))
                return new(InvoiceApiDecision.Success,"inquiry_authority_rejected");
            if(!root.TryGetProperty("result",out var result))
                return new(InvoiceApiDecision.Success,"inquiry_result_missing");
            JsonElement results;
            if(result.ValueKind==JsonValueKind.Array)
                results=result;
            else if(result.ValueKind==JsonValueKind.Object &&
                    result.TryGetProperty("data",out var inner) &&
                    inner.ValueKind==JsonValueKind.Array)
                results=inner;
            else
                return new(InvoiceApiDecision.Success,"inquiry_result_shape_unrecognized");
            if(results.GetArrayLength()==0)
                return new(InvoiceApiDecision.Success,"inquiry_no_results");
            var expected=new InvoiceReconciliationIdentity(operationId,owner.Value,taxpayerId,
                memoryId,uid,packetType,memoryCode,referenceNumber);
            var verdict=Phase10InquiryReconciliation.Evaluate(expected,results.GetRawText());
            var report=LegacyAuthorityInquiryReportParser.Parse(expected,results.GetRawText(),DateTime.UtcNow);
            // Read-only until authoritative final acceptance semantics are separately verified.
            // Never transition Acknowledged into fiscally Accepted merely from a status string.
            if(verdict.Outcome==InvoiceReconciliationOutcome.CorrelatedPendingReview &&
               verdict.MatchedReference==referenceNumber)
            {
                var observed=DateTime.UtcNow;
                var digest=Phase10DispatchEvidenceRepository.EvidenceHash(
                    "inquiry_reference_correlated",referenceNumber,observed);
                var written=await evidence.AppendReferenceInquiryEvidenceAsync(
                    operationId,owner.Value,taxpayerId,attemptId,stateRevision,
                    referenceNumber,digest,observed,ct);
                if(written.Status!=Phase10DispatchEvidenceStatus.Recorded)
                    return new(InvoiceApiDecision.Success,"inquiry_evidence_conflict");
                if(report is not null && !await new LegacyAuthorityReportRepository(db).AppendAsync(
                    owner.Value,taxpayerId,invoiceId,operationId,referenceNumber,report,ct))
                    return new(InvoiceApiDecision.Success,"inquiry_evidence_conflict");
            }
            return new(InvoiceApiDecision.Success,verdict.SafeCode,verdict.MatchedReference,report);
        }
        catch(HttpRequestException){return new(InvoiceApiDecision.Success,"inquiry_network_unavailable");}
        catch(JsonException){return new(InvoiceApiDecision.Success,"inquiry_response_invalid");}
        catch(OperationCanceledException)when(!ct.IsCancellationRequested){
            return new(InvoiceApiDecision.Success,"inquiry_timeout");
        }
    }
}
