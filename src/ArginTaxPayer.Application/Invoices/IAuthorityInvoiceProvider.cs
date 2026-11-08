using ArginTaxPayer.Domain.Invoices;

namespace ArginTaxPayer.Application.Invoices;

/// <summary>
/// Versioned authority protocol boundary. Implementations own vendor DTOs and
/// cryptographic/wire details; invoice and legal rules stay SDK-independent.
/// </summary>
public interface IAuthorityInvoiceProvider
{
    string Family { get; }
    string Version { get; }
    string WireContract { get; }
    bool Supports(TaxAuthorityEnvironment environment);
}
