using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Domain.Taxpayers;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>
/// Read-only, server-owned fiscal-memory authority classification.
/// This is not permission to send: a future transport must additionally verify
/// the immutable dispatch operation's environment, protocol, keys and consent.
/// The caller must already have authenticated the actor and verified its scope.
/// </summary>
public sealed class Phase18PersistedAuthorityEnvironmentResolver(ArginTaxPayerDbContext db)
{
    public async Task<TaxAuthorityEnvironment> ResolveAsync(
        Guid customerUserId, Guid taxpayerId, Guid invoiceId, Guid memoryId,
        CancellationToken ct = default)
    {
        if (customerUserId == Guid.Empty || taxpayerId == Guid.Empty ||
            invoiceId == Guid.Empty || memoryId == Guid.Empty)
            return TaxAuthorityEnvironment.Unknown;

        // Do not use the invoice snapshot, request environment, ASPNETCORE_ENVIRONMENT
        // or an arbitrary URL. Bind the memory to the same persisted owner/taxpayer
        // and existing invoice, and refuse inactive membership.
        var stored = await (
            from memory in db.FiscalMemories.AsNoTracking()
            join taxpayer in db.TaxpayerProfiles.AsNoTracking()
                on memory.TaxpayerId equals taxpayer.Id
            join invoice in db.InvoiceDocuments.AsNoTracking()
                on taxpayer.Id equals invoice.TaxpayerId
            where memory.Id == memoryId &&
                  memory.TaxpayerId == taxpayerId &&
                  taxpayer.CustomerUserId == customerUserId &&
                  invoice.Id == invoiceId &&
                  invoice.CustomerUserId == customerUserId &&
                  db.CustomerTaxpayerMemberships.Any(m =>
                      m.CustomerUserId == customerUserId &&
                      m.TaxpayerId == taxpayerId &&
                      m.UnlinkedAtUtc == null)
            select memory.AuthorityEnvironment
        ).SingleOrDefaultAsync(ct);

        return ToAuthorityEnvironment(stored);
    }

    public static TaxAuthorityEnvironment ToAuthorityEnvironment(
        FiscalConnectionEnvironment? persisted) => persisted switch
        {
            // The enum backing values intentionally differ:
            // FiscalConnectionEnvironment Production=1 / Sandbox=2,
            // TaxAuthorityEnvironment Sandbox=1 / Production=2.
            FiscalConnectionEnvironment.Production => TaxAuthorityEnvironment.Production,
            FiscalConnectionEnvironment.Sandbox => TaxAuthorityEnvironment.Sandbox,
            _ => TaxAuthorityEnvironment.Unknown
        };
}
