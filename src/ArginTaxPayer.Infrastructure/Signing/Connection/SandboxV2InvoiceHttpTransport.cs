using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace ArginTaxPayer.Infrastructure.Signing.Connection;

/// <summary>
/// Sandbox-only SDK v2 REST transport primitive. Deliberately not registered
/// behind any invoice HTTP endpoint until durable prepared signed-packet
/// admission, certificate chain validation and owner confirmation exist.
/// Explicit operator config; production hosts, redirects, fallback and
/// automatic retries are prohibited. One POST per call, result indeterminate
/// unless a correlated provider reference is positively established.
/// </summary>
internal sealed class SandboxV2InvoiceHttpTransport(
    IConfiguration configuration, Func<HttpClient>? testClientFactory=null)
{
    internal sealed record Result(bool Acknowledged,string SafeCode,
        string? ReferenceNumber=null);

    internal static bool TryResolveSandboxBase(IConfiguration config,out Uri? uri)
    {
        uri=null;
        if(config["SandboxInvoiceV2:Enabled"]!="true" ||
           config["SandboxInvoiceV2:OperatorApprovedEndpoint"]!="true")
            return false;
        string? supplied=config["SandboxInvoiceV2:ApiBaseUrl"];
        if(!Uri.TryCreate(supplied,UriKind.Absolute,out var value) ||
            value.Scheme!=Uri.UriSchemeHttps ||
            value.Host!="sandboxrc.tax.gov.ir" || value.Port!=443 ||
            value.UserInfo.Length>0 ||
            !string.IsNullOrEmpty(value.Query) ||
            !string.IsNullOrEmpty(value.Fragment) ||
            value.AbsolutePath is not ("/" or "/req/"))
            return false;
        uri=value;
        return true;
    }

    internal async Task<string?> FetchNonceAsync(CancellationToken ct)
    {
        if(!TryResolveSandboxBase(configuration,out var endpoint)||endpoint is null)
            return null;
        using var client=testClientFactory?.Invoke() ?? NewClient();
        using var request=new HttpRequestMessage(HttpMethod.Get,
            new Uri(endpoint,"api/v2/nonce"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        try{
            using var response=await client.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead,ct);
            if(!response.IsSuccessStatusCode)return null;
            using var json=await ParseBounded(response,ct);
            if(!json.RootElement.TryGetProperty("nonce",out var value) ||
                value.ValueKind!=JsonValueKind.String)return null;
            var nonce=value.GetString();
            return nonce is {Length:>8 and <2048} &&
                   nonce.All(c=>c>=33&&c<=126) ? nonce : null;
        }catch(HttpRequestException){return null;}
         catch(JsonException){return null;}
         catch(OperationCanceledException) when(!ct.IsCancellationRequested){return null;}
    }

    /// <summary>
    /// Network call MUST be made only after a separately verified durable
    /// Sandbox-only outbox claim with a persisted immutable JWE packet.
    /// This method does not implement that claim and is intentionally unreachable
    /// from app endpoints until the final orchestration and incident policy ship.
    /// </summary>
    internal async Task<Result> PostPersistedPacketOnceAsync(
        string protectedJwePayload,string fiscalCode,string requestTraceId,
        string signedNonceBearer,CancellationToken ct)
    {
        if(!TryResolveSandboxBase(configuration,out var endpoint)||endpoint is null)
            return new(false,"sandbox_v2_transport_disabled");
        if(fiscalCode is not {Length:6} ||
            !fiscalCode.All(c=>char.IsAsciiLetterOrDigit(c)) ||
            !Guid.TryParse(requestTraceId,out var uid) ||
            uid==Guid.Empty ||
            protectedJwePayload is not {Length:>100 and <3000000} ||
            !IsCompactJwe(protectedJwePayload) ||
            signedNonceBearer is not {Length:>200 and <25000} ||
            signedNonceBearer.Any(char.IsWhiteSpace) ||
            !IsCompactRs256Token(signedNonceBearer))
            return new(false,"sandbox_v2_packet_invalid");

        using var http=testClientFactory?.Invoke() ?? NewClient();
        using var req=new HttpRequestMessage(HttpMethod.Post,
            new Uri(endpoint,"api/v2/invoice")){
            Content=JsonContent.Create(new[]{
                new{payload=protectedJwePayload,header=new{
                    requestTraceId,fiscalId=fiscalCode}}
            })
        };
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",signedNonceBearer);
        try{
            using var response=await http.SendAsync(req,
                HttpCompletionOption.ResponseHeadersRead,ct);
            // Any uncorrelated/HTTP status is unknown, NOT proof of nonreceipt.
            if(response.StatusCode!=HttpStatusCode.OK)return new(false,"sandbox_v2_outcome_indeterminate");
            using var doc=await ParseBounded(response,ct);
            if(!doc.RootElement.TryGetProperty("result",out var rows) ||
                rows.ValueKind!=JsonValueKind.Array ||
                rows.GetArrayLength()!=1)
                return new(false,"sandbox_v2_outcome_indeterminate");
            var row=rows[0];
            if(!row.TryGetProperty("uid",out var echoed) ||
                echoed.ValueKind!=JsonValueKind.String ||
                echoed.GetString()!=requestTraceId ||
                !row.TryGetProperty("referenceNumber",out var reference) ||
                reference.ValueKind!=JsonValueKind.String)
                return new(false,"sandbox_v2_outcome_indeterminate");
            var number=reference.GetString();
            if(number is not {Length:>0 and <=128} ||
                !number.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_'))
                return new(false,"sandbox_v2_outcome_indeterminate");
            return new(true,"sandbox_v2_reference_acknowledged",number);
        }catch(HttpRequestException){return new(false,"sandbox_v2_outcome_indeterminate");}
         catch(JsonException){return new(false,"sandbox_v2_outcome_indeterminate");}
         catch(OperationCanceledException) when(!ct.IsCancellationRequested){
            return new(false,"sandbox_v2_outcome_indeterminate");
         }
    }

    private static bool IsCompactRs256Token(string signedToken)
    {
        var segments=signedToken.Split('.');
        if(segments.Length!=3 || segments.Any(s=>s.Length==0 ||
            s.Any(ch=>!(ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or
                >= '0' and <= '9' or '-' or '_'))))
            return false;
        try{
            var header=Convert.FromBase64String(segments[0]
                .Replace('-','+').Replace('_','/')
                .PadRight((segments[0].Length+3)/4*4,'='));
            using var document=JsonDocument.Parse(header);
            return document.RootElement.ValueKind==JsonValueKind.Object &&
                document.RootElement.TryGetProperty("alg",out var algorithm) &&
                algorithm.GetString()=="RS256" &&
                segments[2].Length==342;
        }catch(Exception e) when(e is FormatException or JsonException or
            InvalidOperationException or ArgumentException){
            return false;
        }
    }

    // Require actual JOSE compact shape and protected RSA-OAEP-256/A256GCM
    // headers; a dotted arbitrary string must never pass into send transport.
    // Full cryptographic proof remains mandatory in the signed-outbox layer.
    private static bool IsCompactJwe(string candidate)
    {
        var parts=candidate.Split('.');
        if(parts.Length!=5 ||
           parts.Any(part=>part.Length==0 ||
              part.Any(ch=>!(ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or
                  >= '0' and <= '9' or '-' or '_'))))
            return false;
        try {
            byte[] encodedHeader=Convert.FromBase64String(
                parts[0].Replace('-','+').Replace('_','/')
                    .PadRight((parts[0].Length+3)/4*4,'='));
            using var json=JsonDocument.Parse(encodedHeader);
            if(json.RootElement.ValueKind!=JsonValueKind.Object ||
               !json.RootElement.TryGetProperty("alg",out var alg) ||
               alg.GetString()!="RSA-OAEP-256" ||
               !json.RootElement.TryGetProperty("enc",out var enc) ||
               enc.GetString()!="A256GCM" ||
               !json.RootElement.TryGetProperty("kid",out var kid) ||
               kid.ValueKind!=JsonValueKind.String ||
               kid.GetString() is not {Length:>0 and <=128})
                return false;
            // Base64url encoded 96-bit IV and 128-bit GCM tag.
            if(parts[2].Length!=16 || parts[4].Length!=22)
                return false;
            return parts[1].Length>=300 && parts[3].Length>0;
        }catch(Exception e) when(e is FormatException or JsonException or
            InvalidOperationException or ArgumentException){
            return false;
        }
    }

    private static HttpClient NewClient()
    {
        var handler=new HttpClientHandler{
            AllowAutoRedirect=false,UseCookies=false,
            AutomaticDecompression=DecompressionMethods.None
        };
        return new HttpClient(handler,disposeHandler:true){
            Timeout=TimeSpan.FromSeconds(15),
            MaxResponseContentBufferSize=65536
        };
    }

    private static async Task<JsonDocument> ParseBounded(
        HttpResponseMessage response,CancellationToken ct)
    {
        if(response.Content.Headers.ContentLength is >65536)
            throw new JsonException("Oversize provider response");
        await using var input=await response.Content.ReadAsStreamAsync(ct);
        using var ms=new MemoryStream();
        var buffer=new byte[4096];
        while(true){
            var count=await input.ReadAsync(buffer.AsMemory(),ct);
            if(count==0)break;
            if(ms.Length+count>65536)throw new JsonException("Oversize provider response");
            ms.Write(buffer,0,count);
        }
        ms.Position=0;
        return await JsonDocument.ParseAsync(ms,
            new JsonDocumentOptions{MaxDepth=16},ct);
    }
}
