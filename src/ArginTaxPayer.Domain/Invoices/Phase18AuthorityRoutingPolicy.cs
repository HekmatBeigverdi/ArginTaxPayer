namespace ArginTaxPayer.Domain.Invoices;

/// <summary>
/// Phase18 fail-closed environment consistency contract for future persisted
/// authority operation binding. Policy alone does not authorize network IO.
/// The authoritative values must come from a trusted database read, not JSON.
/// </summary>
public enum TaxAuthorityEnvironment { Unknown = 0, Sandbox = 1, Production = 2 }

public readonly record struct AuthorityOperationBinding(
    Guid CustomerUserId, Guid TaxpayerId, Guid InvoiceId, Guid FiscalMemoryId,
    TaxAuthorityEnvironment Environment);

public static class Phase18AuthorityRoutingPolicy
{
    public static bool CanUseMemory(AuthorityOperationBinding persistedOperation,
        AuthorityOperationBinding persistedMemory)
    {
        if (persistedOperation.CustomerUserId == Guid.Empty ||
            persistedOperation.TaxpayerId == Guid.Empty ||
            persistedOperation.InvoiceId == Guid.Empty ||
            persistedOperation.FiscalMemoryId == Guid.Empty ||
            persistedOperation.Environment is TaxAuthorityEnvironment.Unknown ||
            persistedMemory.Environment is TaxAuthorityEnvironment.Unknown)
            return false;
        return persistedOperation.CustomerUserId == persistedMemory.CustomerUserId &&
               persistedOperation.TaxpayerId == persistedMemory.TaxpayerId &&
               persistedOperation.FiscalMemoryId == persistedMemory.FiscalMemoryId &&
               persistedOperation.Environment == persistedMemory.Environment;
    }

    public static bool CanUseProvider(AuthorityOperationBinding persistedOperation,
        AuthorityOperationBinding persistedMemory,
        TaxAuthorityEnvironment registeredProvider)
        => CanUseMemory(persistedOperation, persistedMemory) &&
           registeredProvider is not TaxAuthorityEnvironment.Unknown &&
           persistedOperation.Environment == registeredProvider;

    public static bool CanInheritLegalSuccessor(AuthorityOperationBinding predecessor,
        AuthorityOperationBinding successor)
        => predecessor.InvoiceId != Guid.Empty &&
           successor.InvoiceId != Guid.Empty &&
           predecessor.InvoiceId != successor.InvoiceId &&
           CanUseMemory(successor, predecessor with { InvoiceId = successor.InvoiceId });
}
