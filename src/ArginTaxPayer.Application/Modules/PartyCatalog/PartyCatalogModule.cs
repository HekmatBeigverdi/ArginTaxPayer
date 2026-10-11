using ArginTaxPayer.Application.Modules;

namespace ArginTaxPayer.Application.Modules.PartyCatalog;

/// <summary>Architectural boundary declaration; contains no implemented business behavior.</summary>
public sealed class PartyCatalogModule : IModuleDescriptor
{
    public string Name => "PartyCatalog";
    public string Ownership => "Counterparties, products and official units";
    public string SecurityInvariant => "No direct fiscal submission mutations";
}
