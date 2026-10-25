using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using ArginTaxPayer.Infrastructure.Signing.Offline;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Microsoft.Extensions.Configuration;

namespace ArginTaxPayer.Infrastructure.Signing.Connection;

internal sealed record LegacySandboxEncryptionKey(string KeyId, byte[] PublicSpkiDer)
    : IDisposable
{
    public void Dispose() => CryptographicOperations.ZeroMemory(PublicSpkiDer);
}

/// <summary>
/// Reads the current encryption key only from pinned Sandbox GET_SERVER_INFORMATION.
/// Uses the old PKCS8 transfer signature and never falls back to Production.
/// The response is not a provider invoice acceptance or send authorization.
/// </summary>
internal sealed class LegacySandboxServerEncryptionKeyClient(
    IConfiguration configuration, Func<HttpClient>? clientFactory = null)
{
    internal async Task<LegacySandboxEncryptionKey?> RequestAsync(
        string fiscalMemoryCode, SensitiveBytes privateKey,
        ReadOnlyMemory<byte> publicSpki, CancellationToken ct = default)
    {
        if (!LegacyInvoiceSubmissionHttpTransport.TryResolveSandboxBase(
                configuration, out var baseUri) || baseUri is null ||
            fiscalMemoryCode is not { Length: 6 } ||
            fiscalMemoryCode.Any(c => !char.IsAsciiLetterOrDigit(c)))
            return null;
        var trace = Guid.NewGuid().ToString("D");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
        var packet = new {
            Uid=Guid.NewGuid().ToString("D"),
            PacketType="GET_SERVER_INFORMATION",
            Retry=false,
            Data=(object?)null,
            EncryptionKeyId=(string?)null,
            SymmetricKey=(string?)null,
            Iv=(string?)null,
            FiscalId=fiscalMemoryCode,
            DataSignature=(string?)null
        };
        var signature = new LegacyOfflineSigningEngine().SignObject(
            privateKey,publicSpki,packet,LegacySigningPurpose.Transfer,
            new Dictionary<string,string>{
                ["requestTraceId"]=trace,["timestamp"]=timestamp
            }).SignatureBase64;
        using var http=clientFactory?.Invoke() ?? new HttpClient(new HttpClientHandler {
            AllowAutoRedirect=false,UseCookies=false,
            AutomaticDecompression=DecompressionMethods.None
        }) { Timeout=TimeSpan.FromSeconds(15),MaxResponseContentBufferSize=262144 };
        using var request=new HttpRequestMessage(HttpMethod.Post,
            new Uri(baseUri,"self-tsp/sync/GET_SERVER_INFORMATION")){
            Content=JsonContent.Create(new {
                Signature=signature,SignatureKeyId=(string?)null,
                Packet=new {
                    packet.Uid,packet.PacketType,packet.Retry,packet.Data,
                    packet.EncryptionKeyId,packet.SymmetricKey,packet.Iv,
                    packet.FiscalId,packet.DataSignature,SignatureKeyId=(string?)null
                }
            })
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("requestTraceId",trace);
        request.Headers.TryAddWithoutValidation("timestamp",timestamp);
        try
        {
            using var response=await http.SendAsync(
                request,HttpCompletionOption.ResponseHeadersRead,ct);
            if(!response.IsSuccessStatusCode ||
                response.Content.Headers.ContentLength is > 262144)
                return null;
            var bytes=await response.Content.ReadAsByteArrayAsync(ct);
            if(bytes.Length>262144)return null;
            using var document=JsonDocument.Parse(bytes,
                new JsonDocumentOptions{MaxDepth=20});
            var root=document.RootElement;
            if(Try(root,"errors",out var errors)&&errors.ValueKind!=JsonValueKind.Null &&
                (errors.ValueKind!=JsonValueKind.Array||errors.GetArrayLength()!=0))
                return null;
            if(!Try(root,"result",out var result)||
               !Try(result,"data",out var data)||
               !Try(data,"publicKeys",out var keys)||
               keys.ValueKind!=JsonValueKind.Array||keys.GetArrayLength()==0)
                return null;
            foreach(var item in keys.EnumerateArray())
            {
                if(!Try(item,"id",out var id)||
                   !Try(item,"key",out var keyElement)||
                   id.ValueKind!=JsonValueKind.String||
                   keyElement.ValueKind!=JsonValueKind.String)
                    continue;
                var name=id.GetString();
                var b64=keyElement.GetString();
                if(name is not { Length: > 0 and <= 128 } ||
                   name.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not '_' and not '-') ||
                   b64 is not {Length: > 100 and < 10000})
                    continue;
                byte[]? der=null;
                try
                {
                    der=Convert.FromBase64String(b64);
                    using var rsa=RSA.Create();
                    rsa.ImportSubjectPublicKeyInfo(der,out var consumed);
                    if(consumed!=der.Length||rsa.KeySize<2048||rsa.KeySize>4096)
                        continue;
                    return new LegacySandboxEncryptionKey(name,der);
                }
                catch(Exception e) when(e is CryptographicException or FormatException)
                {
                    // Reject malformed authority key without returning it.
                }
                finally { if(der is not null && der.Length>0)
                    CryptographicOperations.ZeroMemory(der); }
            }
            return null;
        }
        catch(Exception error) when(error is HttpRequestException or JsonException
            or OperationCanceledException && !ct.IsCancellationRequested)
        {
            return null;
        }
    }

    private static bool Try(JsonElement value,string field,out JsonElement result)
    {
        result=default;
        if(value.ValueKind!=JsonValueKind.Object)return false;
        foreach(var property in value.EnumerateObject())
            if(property.Name.Equals(field,StringComparison.OrdinalIgnoreCase)){
                result=property.Value;return true;
            }
        return false;
    }
}
