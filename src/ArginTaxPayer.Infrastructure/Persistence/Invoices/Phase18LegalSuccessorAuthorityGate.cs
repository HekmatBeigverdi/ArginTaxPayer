using ArginTaxPayer.Domain.Invoices;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>
/// Read-only environment inheritance for cancellation/correction/sales-return
/// successors. The predecessor operation and memory must match the requested
/// provider, and the successor must be a scoped legal child. This does not
/// prove authority SUCCESS or authorize a legal successor submission.
/// </summary>
public sealed class Phase18LegalSuccessorAuthorityGate(
    ArginTaxPayerDbContext db, Phase18PersistedAuthorityOperationGate gate)
{
    public async Task<AuthorityOperationBinding?> InheritAsync(
        Guid predecessorOperationId, Guid customerUserId, Guid taxpayerId,
        Guid predecessorInvoiceId, Guid successorInvoiceId,
        TaxAuthorityEnvironment registeredProvider, CancellationToken ct = default)
    {
        if (predecessorInvoiceId == Guid.Empty ||
            successorInvoiceId == Guid.Empty ||
            predecessorInvoiceId == successorInvoiceId ||
            !Enum.IsDefined(registeredProvider) ||
            registeredProvider == TaxAuthorityEnvironment.Unknown)
            return null;

        var predecessor = await gate.ResolveAsync(predecessorOperationId,
            customerUserId, taxpayerId, predecessorInvoiceId, ct);
        if (predecessor is not { } original ||
            original.Environment != registeredProvider)
            return null;

        var successorExists = await db.InvoiceDocuments.AsNoTracking().AnyAsync(i =>
            i.Id == successorInvoiceId &&
            i.CustomerUserId == customerUserId &&
            i.TaxpayerId == taxpayerId &&
            i.Subject != InvoiceSubject.Original, ct);
        if (!successorExists)
            return null;

        var inherited = original with { InvoiceId = successorInvoiceId };
        return Phase18AuthorityRoutingPolicy.CanInheritLegalSuccessor(
            original, inherited) ? inherited : null;
    }
}
