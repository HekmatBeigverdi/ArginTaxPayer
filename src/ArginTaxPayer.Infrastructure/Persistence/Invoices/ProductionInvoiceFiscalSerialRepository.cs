using System.Data.Common;
using System.Globalization;
using ArginTaxPayer.Domain.Invoices;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

internal sealed record ProductionFiscalReservation(Guid Id, string FiscalTaxId, long Serial);

/// <summary>
/// Called only inside the producer's transaction, after locking the exact owned
/// active Production memory. Reservations and outbox commit or roll back together.
/// A new unused memory starts at 1. For previously used memories the operator
/// supplies {Production|Sandbox}InvoiceLegacy:LastUsedSerial:{memory UUID in N format} before first send.
/// Known external fiscal identifiers are upper bounds, never copied as new IDs.
/// </summary>
internal static class ProductionInvoiceFiscalSerialRepository
{
    internal static long NextSerial(long localMaximum, string? configuredLastUsed,
        IEnumerable<string> knownTaxIds, string memoryCode)
    {
        const long maximum = 0xffffffffffL;
        if (localMaximum < 0 || localMaximum >= maximum)
            throw new ArgumentException("Production fiscal serial exhausted or invalid.");
        var upperBound = localMaximum;
        if (configuredLastUsed is not null)
        {
            if (!long.TryParse(configuredLastUsed, NumberStyles.None, CultureInfo.InvariantCulture,
                    out var configured) || configured < 0 || configured >= maximum)
                throw new ArgumentException("Invalid Production last-used fiscal serial.");
            upperBound = Math.Max(upperBound, configured);
        }
        foreach (var id in knownTaxIds)
        {
            if (id.Length == 22 && id.StartsWith(memoryCode, StringComparison.Ordinal) &&
                id.AsSpan(6,15).ToString().All(Uri.IsHexDigit) && char.IsAsciiDigit(id[21]) &&
                long.TryParse(id.AsSpan(11,10), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var serial))
                upperBound = Math.Max(upperBound, serial);
        }
        if (upperBound >= maximum) throw new ArgumentException("Production fiscal serial exhausted.");
        return upperBound + 1;
    }

    internal static async Task<ProductionFiscalReservation> ReserveAsync(
        DbConnection connection, DbTransaction transaction, StoredInvoice invoice,
        Guid memoryId, string memoryCode, int environment, string? configuredLastUsed,
        IEnumerable<string> knownTaxIds, CancellationToken ct)
    {
        await using var existing = Phase18ProductionInvoicePreparationService.Command(connection,transaction,"""
SELECT id,fiscal_tax_id,serial,invoice_revision,approved_snapshot_sha256,fiscal_memory_id
FROM authority_invoice_fiscal_reservations
WHERE customer_user_id=@owner AND taxpayer_id=@taxpayer AND invoice_id=@invoice
""",("owner",invoice.CustomerUserId),("taxpayer",invoice.TaxpayerId),("invoice",invoice.Id));
        await using(var reader=await existing.ExecuteReaderAsync(ct))
        {
            if(await reader.ReadAsync(ct))
            {
                if(reader.GetInt64(3)!=invoice.Revision || reader.GetString(4)!=invoice.SnapshotSha256 ||
                    reader.GetGuid(5)!=memoryId)
                    throw new InvalidOperationException("Immutable Production reservation differs from invoice.");
                return new(reader.GetGuid(0),reader.GetString(1),reader.GetInt64(2));
            }
        }
        var maximum=await Phase18ProductionInvoicePreparationService.Scalar(connection,transaction,"""
SELECT COALESCE(MAX(serial),0) FROM authority_invoice_fiscal_reservations WHERE fiscal_memory_id=@memory
""",ct,("memory",memoryId));
        var serialNumber=NextSerial(Convert.ToInt64(maximum),configuredLastUsed,knownTaxIds,memoryCode);
        var fiscalTaxId=Phase10FiscalInvoiceIdentifier.Generate(memoryCode,serialNumber,invoice.IssueDateUtc);
        var id=Guid.NewGuid();
        await using var insert=Phase18ProductionInvoicePreparationService.Command(connection,transaction,"""
INSERT INTO authority_invoice_fiscal_reservations
(id,customer_user_id,taxpayer_id,fiscal_memory_id,invoice_id,invoice_revision,
 approved_snapshot_sha256,serial,fiscal_tax_id,issue_date_utc,created_at_utc,authority_environment)
VALUES(@id,@owner,@taxpayer,@memory,@invoice,@revision,@hash,@serial,@taxid,@issue,@now,@environment)
""",("id",id),("owner",invoice.CustomerUserId),("taxpayer",invoice.TaxpayerId),
            ("memory",memoryId),("invoice",invoice.Id),("revision",invoice.Revision),
            ("hash",invoice.SnapshotSha256),("serial",serialNumber),("taxid",fiscalTaxId),
            ("issue",invoice.IssueDateUtc),("now",DateTime.UtcNow),("environment",environment));
        await insert.ExecuteNonQueryAsync(ct);
        return new(id,fiscalTaxId,serialNumber);
    }
}
