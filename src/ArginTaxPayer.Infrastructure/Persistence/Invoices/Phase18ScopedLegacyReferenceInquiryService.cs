using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Application.Invoices;
using ArginTaxPayer.Infrastructure.Signing.Connection;
using ArginTaxPayer.Infrastructure.Signing.Offline;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public sealed record Phase18ScopedInquiryResult(string Code, string? ReferenceNumber = null,
    LegacyAuthorityInquiryReport? AuthorityResult = null);

/// <summary>
/// At-most-one read-only inquiry per explicit invocation, never invoice POST.
/// Uses the stored acknowledged receipt, selected memory key and persisted
/// operation environment. A response is not fiscal acceptance until separate
/// reconciliation/evidence is completed.
/// </summary>
internal sealed class Phase18ScopedLegacyReferenceInquiryService(
    ArginTaxPayerDbContext db,
    Phase18PersistedAuthorityOperationGate gate,
    Phase18AuthorityEndpointRoutingService routes,
    DatabaseLegacySigningKeyLoader keys,
    LegacyInvoiceAccessTokenClient tokens,
    Phase18ProductionAuthorityReportRepository reports)
{
    internal async Task<Phase18ScopedInquiryResult> InquireAsync(
        Guid operationId, Guid customerUserId, Guid taxpayerId, Guid invoiceId,
        TaxAuthorityEnvironment provider, CancellationToken ct = default)
    {
        var binding = await gate.ResolveAsync(operationId, customerUserId,
            taxpayerId, invoiceId, ct);
        if (binding is not { } trusted || trusted.Environment != provider)
            return new("authority_environment_mismatch");

        var endpoint = await routes.ResolveAsync(operationId, customerUserId,
            taxpayerId, invoiceId, provider, "INQUIRY_BY_REFERENCE_NUMBER", ct);
        var tokenEndpoint = await routes.ResolveAsync(operationId, customerUserId,
            taxpayerId, invoiceId, provider, "GET_TOKEN", ct);
        if (endpoint is null || tokenEndpoint is null ||
            endpoint.Host != tokenEndpoint.Host)
            return new("reference_inquiry_route_disabled");

        var conn = db.Database.GetDbConnection();
        var opened = conn.State != ConnectionState.Open;
        string? reference = null;
        try
        {
            if (opened) await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
SELECT r.authority_reference
FROM invoice_dispatch_receipts r
JOIN invoice_dispatch_operations d ON d.id=r.operation_id
JOIN invoice_dispatch_attempts a ON a.id=r.attempt_id AND a.operation_id=d.id
WHERE d.id=@operation AND d.customer_user_id=@customer
 AND d.taxpayer_id=@taxpayer AND d.invoice_id=@invoice
 AND d.fiscal_memory_id=@memory AND d.authority_environment=@environment
 AND d.state=5 AND r.receipt_kind=1 AND r.authority_reference IS NOT NULL
 AND a.attempt_number=1
 ORDER BY r.observed_at_utc DESC LIMIT 1
""";
            foreach (var (name,value) in new (string,object)[] {
                ("operation",operationId),("customer",customerUserId),
                ("taxpayer",taxpayerId),("invoice",invoiceId),
                ("memory",trusted.FiscalMemoryId),
                ("environment",provider == TaxAuthorityEnvironment.Production ? 1 : 2)
            })
            {
                var p=cmd.CreateParameter();p.ParameterName=name;p.Value=value;
                cmd.Parameters.Add(p);
            }
            reference=await cmd.ExecuteScalarAsync(ct) as string;
        }
        finally { if (opened) await conn.CloseAsync(); }
        if (reference is not { Length: > 0 and <= 128 } ||
            reference.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not '_' and not '-'))
            return new("acknowledged_reference_not_found");

        var memoryCode=await db.FiscalMemories.AsNoTracking()
            .Where(m=>m.Id==trusted.FiscalMemoryId && m.TaxpayerId==taxpayerId)
            .Select(m=>m.MemoryIdentifier).SingleOrDefaultAsync(ct);
        if (memoryCode is not { Length: 6 })
            return new("fiscal_memory_identifier_unavailable");

        using var key=await keys.RecoverAsync(customerUserId,taxpayerId,
            trusted.FiscalMemoryId,ct);
        if (key is null)
            return new("scoped_signing_key_unavailable");

        var token=await tokens.RequestVerifiedEndpointAsync(tokenEndpoint,
            memoryCode,key.PrivateKey,key.PublicSpkiDer,ct);
        if (!token.Authenticated || token.AccessToken is null)
            return new("authority_token_unavailable");

        var trace=Guid.NewGuid().ToString("D");
        var stamp=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        var packet=new {
            Uid=Guid.NewGuid().ToString("D"),
            PacketType="INQUIRY_BY_REFERENCE_NUMBER",Retry=false,
            Data=new { ReferenceNumber=new[]{reference} },
            EncryptionKeyId=(string?)null,SymmetricKey=(string?)null,
            Iv=(string?)null,FiscalId=memoryCode,DataSignature=(string?)null
        };
        var signature=new LegacyOfflineSigningEngine().SignObject(
            key.PrivateKey,key.PublicSpkiDer,packet,LegacySigningPurpose.Transfer,
            new Dictionary<string,string> {
                ["requestTraceId"]=trace,["timestamp"]=stamp,
                ["Authorization"]="Bearer "+token.AccessToken
            }).SignatureBase64;

        using var http=new HttpClient(new HttpClientHandler {
            AllowAutoRedirect=false,UseCookies=false,
            AutomaticDecompression=DecompressionMethods.None
        }) { Timeout=TimeSpan.FromSeconds(15),MaxResponseContentBufferSize=262144 };
        using var request=new HttpRequestMessage(HttpMethod.Post,endpoint) {
            Content=JsonContent.Create(new {
                Signature=signature,SignatureKeyId=(string?)null,
                Packet=new {
                    packet.Uid,packet.PacketType,packet.Retry,packet.Data,
                    packet.EncryptionKeyId,packet.SymmetricKey,packet.Iv,packet.FiscalId,
                    packet.DataSignature,SignatureKeyId=(string?)null
                }
            },options:new JsonSerializerOptions(JsonSerializerDefaults.Web))
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token.AccessToken);
        request.Headers.TryAddWithoutValidation("requestTraceId",trace);
        request.Headers.TryAddWithoutValidation("timestamp",stamp);
        try
        {
            using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
            if (!response.IsSuccessStatusCode)
                return new(response.StatusCode switch {
                    HttpStatusCode.Unauthorized=>"inquiry_http_401",
                    HttpStatusCode.Forbidden=>"inquiry_http_403",
                    HttpStatusCode.NotFound=>"inquiry_http_404",
                    HttpStatusCode.TooManyRequests=>"inquiry_http_429",
                    _=>"inquiry_http_other"
                });
            if (response.Content.Headers.ContentLength is > 262144)
                return new("inquiry_response_too_large");
            var body=await response.Content.ReadAsByteArrayAsync(ct);
            if (body.Length>262144) return new("inquiry_response_too_large");
            using var document=JsonDocument.Parse(body,new JsonDocumentOptions{MaxDepth=16});
            var root=document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return new("inquiry_response_unrecognized");
            if (root.TryGetProperty("errors",out var errors) &&
                errors.ValueKind != JsonValueKind.Null &&
                (errors.ValueKind != JsonValueKind.Array || errors.GetArrayLength()>0))
                return new("inquiry_authority_rejected");
            if (!root.TryGetProperty("result",out var result))
                return new("inquiry_result_missing");
            JsonElement rows;
            if (result.ValueKind==JsonValueKind.Array)
                rows=result;
            else if (result.ValueKind==JsonValueKind.Object &&
                result.TryGetProperty("data",out var data) &&
                data.ValueKind==JsonValueKind.Array)
                rows=data;
            else
                return new("inquiry_result_unrecognized");
            // Scope every response row to the original durable invoice UID,
            // memory ID and acknowledged reference. Even a correlated SUCCESS
            // is pending separate official final-status validation.
            var expected=new InvoiceReconciliationIdentity(operationId,
                customerUserId,taxpayerId,trusted.FiscalMemoryId,
                operationId.ToString("D"),"INVOICE.V01",memoryCode,reference);
            var verdict=Phase10InquiryReconciliation.Evaluate(expected,rows.GetRawText());
            var report=LegacyAuthorityInquiryReportParser.Parse(
                expected,rows.GetRawText(),DateTime.UtcNow);
            // A correlated, explicit authority rejection may be copied into a
            // separate editable draft. This does not mutate or resend the
            // acknowledged Production invoice, and is not fiscal acceptance.
            if(report is not null && !await reports.AppendAsync(customerUserId,taxpayerId,
                    invoiceId,operationId,reference,report,ct))
                return new("inquiry_report_persist_failed",verdict.MatchedReference,report);
            return new(verdict.SafeCode,verdict.MatchedReference,report);
        }
        catch (HttpRequestException){return new("inquiry_network_unavailable");}
        catch (JsonException){return new("inquiry_response_invalid");}
        catch (OperationCanceledException) when(!ct.IsCancellationRequested){
            return new("inquiry_timeout");
        }
    }
}
