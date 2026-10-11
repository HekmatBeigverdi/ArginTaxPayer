using ArginTaxPayer.Application.Modules;

namespace ArginTaxPayer.Application.Modules.ImportIntegration;

/// <summary>Architectural boundary declaration; contains no implemented business behavior.</summary>
public sealed class ImportIntegrationModule : IModuleDescriptor
{
    public string Name => "ImportIntegration";
    public string Ownership => "Legacy and ArginAccounting import boundaries";
    public string SecurityInvariant => "Historical invoices must never be resent automatically";
}
