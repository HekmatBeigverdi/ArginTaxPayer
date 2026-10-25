namespace ArginTaxPayer.Application.Invoices;

public sealed record Phase10LegacyOutboxWorkResult(
    string Status, string SafeCode, Guid? AttemptId = null, string? ReferenceNumber = null);

public interface IPhase10LegacyOutboxWorker
{
    Task<Phase10LegacyOutboxWorkResult> ProcessInvoiceAsync(
        Guid taxpayerId, Guid invoiceId, CancellationToken cancellationToken = default);
}
