namespace ArginTaxPayer.Application.Signing;

public enum SigningPrivateKeyFormat { Pkcs8Der = 1, Pkcs8Pem = 2, EncryptedPkcs8Der = 3, EncryptedPkcs8Pem = 4 }

public sealed record ImportSigningKeyRequest(Guid TaxpayerId, Guid FiscalMemoryId, Guid OperationId,
    string ProfileId, string ConsentVersion, bool AcceptServerCustody, string? ExistingPublicKeyPem = null);

public interface ISigningKeyImportService
{
    /// <summary>Takes ownership of input and disposes it on every return/exception.
    /// The caller must separately clear the original source buffers it used to construct input.</summary>
    Task<SigningKeyGenerationResult> ImportAsync(string? bearer, ImportSigningKeyRequest request,
        SensitiveKeyInput input, CancellationToken ct = default);
}
