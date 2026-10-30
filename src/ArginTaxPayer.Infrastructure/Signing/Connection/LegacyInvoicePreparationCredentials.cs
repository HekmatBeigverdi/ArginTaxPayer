using ArginTaxPayer.Infrastructure.Signing.Protection;

namespace ArginTaxPayer.Infrastructure.Signing.Connection;

internal sealed record LegacyInvoicePreparationCredentials(
    LegacyInvoiceAccessTokenResult Token, LegacySandboxEncryptionKey? EncryptionKey) : IDisposable
{
    public void Dispose() => EncryptionKey?.Dispose();

    internal static async Task<LegacyInvoicePreparationCredentials> RequestAsync(
        LegacyInvoiceAccessTokenClient tokens, LegacySandboxServerEncryptionKeyClient encryption,
        string memoryCode, SensitiveBytes privateKey, ReadOnlyMemory<byte> publicKey,
        CancellationToken ct)
    {
        // Both calls sign their own request, then wait on independent endpoints.
        // Await both even on failure so the caller can safely dispose its signing key.
        var tokenTask = tokens.RequestAsync(memoryCode, privateKey, publicKey, ct);
        var encryptionTask = encryption.RequestAsync(memoryCode, privateKey, publicKey, ct);
        try
        {
            await Task.WhenAll(tokenTask, encryptionTask);
            return new(await tokenTask, await encryptionTask);
        }
        catch
        {
            if (encryptionTask.IsCompletedSuccessfully)
                encryptionTask.Result?.Dispose();
            throw;
        }
    }
}
