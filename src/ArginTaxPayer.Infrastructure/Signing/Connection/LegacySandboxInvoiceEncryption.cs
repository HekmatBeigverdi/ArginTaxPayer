using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace ArginTaxPayer.Infrastructure.Signing.Connection;

internal sealed record LegacyEncryptedInvoiceData(
    string CiphertextBase64,string EncryptionKeyId,string WrappedKeyBase64,string IvHex);

/// <summary>Archived SDK compatibility: repeat-key XOR, AES-256-GCM with
/// sixteen-byte nonce, OAEP-SHA256 wrapping of uppercase HEX AES key.</summary>
internal static class LegacySandboxInvoiceEncryption
{
    internal static LegacyEncryptedInvoiceData Encrypt(
        JsonElement data,LegacySandboxEncryptionKey authority)
    {
        ArgumentNullException.ThrowIfNull(authority);
        if(authority.KeyId is not {Length: > 0 and <= 128} ||
            authority.PublicSpkiDer.Length < 100)
            throw new ArgumentException("Unverified Sandbox authority encryption key.");
        byte[]? aesKey=null,iv=null,plain=null,xored=null,encryptedKey=null;
        try
        {
            aesKey=RandomNumberGenerator.GetBytes(32);
            iv=new byte[16];
            RandomNumberGenerator.Fill(iv.AsSpan());
            for(var i=0;i<iv.Length;i++)if(iv[i]==0)iv[i]=1;
            plain=JsonSerializer.SerializeToUtf8Bytes(data,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            xored=new byte[Math.Max(plain.Length,aesKey.Length)];
            for(var i=0;i<xored.Length;i++)
                xored[i]=plain.Length < aesKey.Length
                    ?(byte)(plain[i % plain.Length]^aesKey[i])
                    :(byte)(plain[i]^aesKey[i % aesKey.Length]);
            var cipher=new GcmBlockCipher(new AesEngine());
            cipher.Init(true,new AeadParameters(
                new KeyParameter(aesKey),128,iv,Array.Empty<byte>()));
            var result=new byte[cipher.GetOutputSize(xored.Length)];
            try
            {
                var len=cipher.ProcessBytes(xored,0,xored.Length,result,0);
                len+=cipher.DoFinal(result,len);
                using var rsa=RSA.Create();
                rsa.ImportSubjectPublicKeyInfo(authority.PublicSpkiDer,out var consumed);
                if(consumed!=authority.PublicSpkiDer.Length ||rsa.KeySize<2048)
                    throw new CryptographicException("Invalid authority RSA key.");
                var keyHex=Encoding.ASCII.GetBytes(Convert.ToHexString(aesKey));
                try { encryptedKey=rsa.Encrypt(keyHex,RSAEncryptionPadding.OaepSHA256); }
                finally {CryptographicOperations.ZeroMemory(keyHex);}
                return new(Convert.ToBase64String(result,0,len),authority.KeyId,
                    Convert.ToBase64String(encryptedKey),Convert.ToHexString(iv));
            }
            finally {CryptographicOperations.ZeroMemory(result);}
        }
        finally
        {
            if(aesKey!=null)CryptographicOperations.ZeroMemory(aesKey);
            if(iv!=null)CryptographicOperations.ZeroMemory(iv);
            if(plain!=null)CryptographicOperations.ZeroMemory(plain);
            if(xored!=null)CryptographicOperations.ZeroMemory(xored);
            if(encryptedKey!=null)CryptographicOperations.ZeroMemory(encryptedKey);
        }
    }
}
