using ArginTaxPayer.Application.Modules;

namespace ArginTaxPayer.Application.Modules.Invoicing;

/// <summary>Architectural boundary declaration; contains no implemented business behavior.</summary>
public sealed class InvoicingModule : IModuleDescriptor
{
    public string Name => "Invoicing";
    public string Ownership => "Invoice lifecycle and immutable issue snapshots";
    public string SecurityInvariant => "No direct fiscal SDK singleton";
}
