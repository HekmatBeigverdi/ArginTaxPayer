using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace ArginTaxPayer.Infrastructure.Signing.InvoiceSnapshot;

/// <summary>
/// Real offline cryptographic serialization used by modern 2.0.32 SDK:
/// RS256 JWS over canonical invoice bytes, then RSA-OAEP-256/A256GCM JWE.
/// No HTTP, key persistence, fiscal issuance or signing entitlement here.
/// Caller must separately verify the full issuing certificate chain, SDK
/// profile, legal rules, scope, fiscal ID and durable outbox admission.
/// </summary>
public static class Phase10SandboxInvoicePacketCryptography
{
    public static string SignJws(string validatedInvoiceJson, RSA privateKey,
        X509Certificate2 issuedCertificate, DateTime signedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(privateKey);
        ArgumentNullException.ThrowIfNull(issuedCertificate);
        if(signedAtUtc.Kind!=DateTimeKind.Utc ||
           validatedInvoiceJson is not {Length:>2 and <=1048576})
            throw new ArgumentException("Invalid signed invoice input.");
        using var document=JsonDocument.Parse(validatedInvoiceJson);
        if(document.RootElement.ValueKind!=JsonValueKind.Object)
            throw new ArgumentException("Invoice payload must be an object.");
        using var certificatePublic=issuedCertificate.GetRSAPublicKey();
        if(certificatePublic is null ||
           !certificatePublic.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(
               privateKey.ExportSubjectPublicKeyInfo()))
            throw new CryptographicException("Private key is not bound to invoice certificate.");
        // Certificate revocation / chain status must be verified by the higher
        // authority profile before calling this low-level serialization.
        var header=JsonSerializer.Serialize(new Dictionary<string,object>{
            ["alg"]="RS256",
            ["x5c"]=new[]{Convert.ToBase64String(issuedCertificate.RawData)},
            ["sigT"]=signedAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ",
                System.Globalization.CultureInfo.InvariantCulture),
            ["typ"]="jose",
            ["crit"]=new[]{"sigT"},
            ["cty"]="text/plain"
        });
        var signingInput=Base64Url(Encoding.UTF8.GetBytes(header))+"."+
            Base64Url(Encoding.UTF8.GetBytes(validatedInvoiceJson));
        byte[] signature=privateKey.SignData(Encoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        try{return signingInput+"."+Base64Url(signature);}
        finally{CryptographicOperations.ZeroMemory(signature);}
    }

    public static string EncryptJwe(string signedJws,RSA taxAuthorityEncryptionPublicKey,
        string serverEncryptionKeyId)
    {
        ArgumentNullException.ThrowIfNull(taxAuthorityEncryptionPublicKey);
        if(string.IsNullOrWhiteSpace(signedJws) ||
           signedJws.Length>3_000_000 ||
           string.IsNullOrWhiteSpace(serverEncryptionKeyId) ||
           serverEncryptionKeyId.Length>128 ||
           serverEncryptionKeyId.Any(c=>char.IsControl(c)))
            throw new ArgumentException("Invalid JWE envelope inputs.");
        var header=JsonSerializer.Serialize(new Dictionary<string,string>{
            ["alg"]="RSA-OAEP-256",["enc"]="A256GCM",["kid"]=serverEncryptionKeyId
        });
        var encodedHeader=Base64Url(Encoding.UTF8.GetBytes(header));
        byte[] cek=RandomNumberGenerator.GetBytes(32);
        byte[] iv=RandomNumberGenerator.GetBytes(12);
        byte[] plain=Encoding.UTF8.GetBytes(signedJws);
        byte[] cipher=new byte[plain.Length];
        byte[] tag=new byte[16];
        byte[]? encryptedKey=null;
        try{
            encryptedKey=taxAuthorityEncryptionPublicKey.Encrypt(cek,
                RSAEncryptionPadding.OaepSHA256);
            using(var aes=new AesGcm(cek,16))
                aes.Encrypt(iv,plain,cipher,tag,Encoding.ASCII.GetBytes(encodedHeader));
            return string.Join(".",encodedHeader,Base64Url(encryptedKey),
                Base64Url(iv),Base64Url(cipher),Base64Url(tag));
        }
        finally{
            CryptographicOperations.ZeroMemory(cek);
            CryptographicOperations.ZeroMemory(iv);
            CryptographicOperations.ZeroMemory(plain);
            CryptographicOperations.ZeroMemory(cipher);
            CryptographicOperations.ZeroMemory(tag);
            if(encryptedKey is not null)CryptographicOperations.ZeroMemory(encryptedKey);
        }
    }

    private static string Base64Url(ReadOnlySpan<byte> bytes)=>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+','-').Replace('/','_');
}
