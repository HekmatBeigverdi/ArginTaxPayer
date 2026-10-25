using System.Text.Json;
using ArginTaxPayer.Infrastructure.Signing.Protection;

namespace ArginTaxPayer.Infrastructure.Signing.Connection;

internal sealed record LegacyInvoiceOutboxSendResult(
    bool Acknowledged, string SafeCode, string? ReferenceNumber = null);

internal sealed record LegacyInvoicePreparedSend(
    LegacyInvoiceSubmissionPacket Packet, string AccessToken);

/// <summary>
/// In-memory legacy send pipeline used by the eventual Outbox worker. It does
/// not mark a message processed and does not persist evidence; those actions
/// must surround this single external-send boundary transactionally.
/// </summary>
internal sealed class LegacyInvoiceOutboxSendCoordinator(
    LegacyInvoiceAccessTokenClient tokenClient,
    LegacyInvoiceSubmissionHttpTransport transport)
{
    internal async Task<LegacyInvoiceOutboxSendResult> SendAsync(
        string outboxPayloadJson, DatabaseLegacySigningKey signingKey,
        CancellationToken ct = default)
    {
        var prepared = await PrepareAsync(outboxPayloadJson, signingKey, ct);
        if (prepared is null)
            return new(false, "sandbox_legacy_token_unavailable");
        return await SendPreparedAsync(prepared, ct);
    }

    internal async Task<LegacyInvoicePreparedSend?> PrepareAsync(
        string outboxPayloadJson, DatabaseLegacySigningKey signingKey,
        CancellationToken ct = default, string? sandboxClientId = null)
    {
        ArgumentNullException.ThrowIfNull(signingKey);
        var fiscalId = sandboxClientId ?? ReadFiscalId(outboxPayloadJson);
        var token = await tokenClient.RequestAsync(fiscalId, signingKey.PrivateKey,
            signingKey.PublicSpkiDer, ct);
        if (!token.Authenticated || token.AccessToken is null)
            return null;

        var packet = LegacyInvoiceOutboxPacketRebuilder.Rebuild(outboxPayloadJson,
            signingKey.PrivateKey, signingKey.PublicSpkiDer, token.AccessToken, ct,
            sandboxClientId);
        return new(packet, token.AccessToken);
    }

    internal async Task<LegacyInvoiceOutboxSendResult> SendPreparedAsync(
        LegacyInvoicePreparedSend prepared, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        var packet = prepared.Packet;
        var result = await transport.PostAsync(packet.FiscalTaxId, packet.RequestJson,
            packet.TransferSignature, prepared.AccessToken, packet.RequestTraceId,
            packet.Timestamp, ct);
        return new(result.Acknowledged, result.SafeCode, result.ReferenceNumber);
    }

    private static string ReadFiscalId(string payload)
    {
        if (payload is not { Length: > 2 and <= 3_500_000 })
            throw new ArgumentException("Invalid legacy Outbox payload.", nameof(payload));
        using var document = JsonDocument.Parse(payload,
            new JsonDocumentOptions { MaxDepth = 24 });
        if (!document.RootElement.TryGetProperty("fiscalTaxId", out var value) ||
            value.ValueKind != JsonValueKind.String ||
            value.GetString() is not { Length: >= 6 and <= 32 } fiscalId ||
            fiscalId.Any(c => !char.IsAsciiLetterOrDigit(c)))
            throw new ArgumentException("Invalid legacy fiscal identifier.", nameof(payload));
        return fiscalId;
    }
}
