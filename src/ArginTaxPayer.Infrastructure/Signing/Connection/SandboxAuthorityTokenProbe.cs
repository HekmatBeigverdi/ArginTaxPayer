using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Infrastructure.Signing.Offline;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Microsoft.Extensions.Configuration;

namespace ArginTaxPayer.Infrastructure.Signing.Connection;

/// <summary>
/// Opt-in read-only Sandbox GET_TOKEN + GET_FISCAL_INFORMATION probe.
/// An operator must explicitly approve the historically documented test API candidate and configure
/// its pinned base URL. No Production hostname, failover, redirects or invoice send.
/// </summary>
internal sealed class SandboxAuthorityTokenProbe(IConfiguration config, IHttpClientFactory clients)
{
    internal static bool TryResolveBaseUrl(IConfiguration config, out Uri? endpoint)
    {
        endpoint = null;
        if (config["SandboxConnectionTest:Enabled"] != "true" ||
            config["SandboxConnectionTest:OperatorApprovedExperimentalApi"] != "true")
            return false;
        var supplied = config["SandboxConnectionTest:ApiBaseUrl"];
        if (!Uri.TryCreate(supplied, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || uri.Port != 443 ||
            uri.Host != "sandboxrc.tax.gov.ir" ||
            !uri.AbsolutePath.Equals("/req/api/", StringComparison.Ordinal) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
            !string.IsNullOrEmpty(uri.UserInfo)) return false;
        endpoint = uri;
        return true;
    }

    internal async Task<SigningConnectionTestResult> TestAsync(
        string fiscalCode, SensitiveBytes privateKey, byte[] publicSpki, CancellationToken ct)
    {
        SigningConnectionTestResult Result(SigningConnectionTestStatus status,
            FiscalInformationSnapshot? details = null) =>
            SigningConnectionTestResult.Create(status, DateTime.UtcNow) with { FiscalInformation = details };
        if (!TryResolveBaseUrl(config, out var baseUri) || baseUri is null)
            return Result(SigningConnectionTestStatus.UnsupportedProfile);
        if (!System.Text.RegularExpressions.Regex.IsMatch(fiscalCode,
            "^[A-Z0-9]{6,32}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            return Result(SigningConnectionTestStatus.KeyNotReady);
        using var http = clients.CreateClient("phase10-sandbox-authority");
        try
        {
            using var tokenResponse = await SendAsync(http, new Uri(baseUri,
                "self-tsp/sync/GET_TOKEN"), "GET_TOKEN", fiscalCode,
                new { Username = fiscalCode }, privateKey, publicSpki, null, ct);
            if (!TryData(tokenResponse, out var tokenData) ||
                !TryProperty(tokenData, "token", out var tokenProperty) ||
                tokenProperty.ValueKind != JsonValueKind.String ||
                tokenProperty.GetString() is not { Length: > 0 and < 16384 } token ||
                token.Any(ch => ch < 33 || ch > 126))
                return Result(SigningConnectionTestStatus.Indeterminate);
            using var informationResponse = await SendAsync(http, new Uri(baseUri,
                "self-tsp/sync/GET_FISCAL_INFORMATION"), "GET_FISCAL_INFORMATION",
                fiscalCode, fiscalCode, privateKey, publicSpki, token, ct);
            if (!TryData(informationResponse, out var data) ||
                !LegacyTaxAuthorityConnection.TryReadFiscalInformation(data, out var info))
                return Result(SigningConnectionTestStatus.Indeterminate);
            return Result(SigningConnectionTestStatus.Authenticated, info);
        }
        catch (SandboxRejectedException) { return Result(SigningConnectionTestStatus.AuthenticationRejected); }
        catch (SandboxUnavailableException) { return Result(SigningConnectionTestStatus.EndpointUnavailable); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { return Result(SigningConnectionTestStatus.TimedOut); }
        catch (HttpRequestException) { return Result(SigningConnectionTestStatus.EndpointUnavailable); }
        catch (JsonException) { return Result(SigningConnectionTestStatus.Indeterminate); }
        catch (KeyProtectionException) { return Result(SigningConnectionTestStatus.KeyNotReady); }
    }

    private static bool TryProperty(JsonElement element, string field, out JsonElement found)
    {
        found = default;
        if (element.ValueKind != JsonValueKind.Object) return false;
        foreach (var property in element.EnumerateObject())
            if (property.Name.Equals(field, StringComparison.OrdinalIgnoreCase))
            { found = property.Value; return true; }
        return false;
    }

    private static bool TryData(JsonDocument response, out JsonElement data)
    {
        data = default;
        var root = response.RootElement;
        if (TryProperty(root,"errors",out var errors) && errors.ValueKind != JsonValueKind.Null &&
            (errors.ValueKind != JsonValueKind.Array || errors.GetArrayLength() != 0))
            return false;
        return TryProperty(root,"result",out var result) &&
            TryProperty(result,"data",out data) && data.ValueKind != JsonValueKind.Null;
    }

    private static async Task<JsonDocument> SendAsync(HttpClient http, Uri endpoint,
        string type,string fiscalId,object data,SensitiveBytes key,byte[] publicSpki,
        string? token,CancellationToken ct)
    {
        var trace=Guid.NewGuid().ToString("D");
        var stamp=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
        var headers=new Dictionary<string,string>{
            ["requestTraceId"]=trace,["timestamp"]=stamp
        };
        if(token is not null)headers["Authorization"]="Bearer "+token;
        var packet=new{
            Uid=Guid.NewGuid().ToString("D"),PacketType=type,Retry=false,Data=data,
            EncryptionKeyId=(string?)null,SymmetricKey=(string?)null,
            Iv=(string?)null,FiscalId=fiscalId,DataSignature=(string?)null
        };
        var signature=new LegacyOfflineSigningEngine().SignObject(
            key,publicSpki,packet,LegacySigningPurpose.Transfer,headers).SignatureBase64;
        using var request=new HttpRequestMessage(HttpMethod.Post,endpoint){
            Content=JsonContent.Create(new {
                Signature=signature,SignatureKeyId=(string?)null,
                Packet=new {
                    packet.Uid,packet.PacketType,packet.Retry,packet.Data,
                    packet.EncryptionKeyId,packet.SymmetricKey,packet.Iv,
                    packet.FiscalId,packet.DataSignature,SignatureKeyId=(string?)null
                }
            },options:new JsonSerializerOptions(JsonSerializerDefaults.Web))
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("requestTraceId",trace);
        request.Headers.TryAddWithoutValidation("timestamp",stamp);
        if(token is not null)
            request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
        using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
        if(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new SandboxRejectedException();
        if(!response.IsSuccessStatusCode)throw new SandboxUnavailableException();
        if(response.Content.Headers.ContentLength is > 262144)throw new JsonException();
        await using var stream=await response.Content.ReadAsStreamAsync(ct);
        using var bounded=new MemoryStream();
        var buffer=new byte[8192];
        try {
            while(true){
                var n=await stream.ReadAsync(buffer.AsMemory(),ct);
                if(n==0)break;
                if(bounded.Length+n>262144)throw new JsonException();
                bounded.Write(buffer,0,n);
            }
            bounded.Position=0;
            return await JsonDocument.ParseAsync(bounded,new JsonDocumentOptions{MaxDepth=16},ct);
        }finally{
            CryptographicOperations.ZeroMemory(buffer);
            CryptographicOperations.ZeroMemory(bounded.GetBuffer().AsSpan(0,(int)bounded.Length));
        }
    }

    private sealed class SandboxRejectedException:Exception{}
    private sealed class SandboxUnavailableException:Exception{}
}
