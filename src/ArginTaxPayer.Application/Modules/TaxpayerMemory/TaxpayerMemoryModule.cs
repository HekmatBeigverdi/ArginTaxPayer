using ArginTaxPayer.Application.Modules;

namespace ArginTaxPayer.Application.Modules.TaxpayerMemory;

/// <summary>Architectural boundary declaration; contains no implemented business behavior.</summary>
public sealed class TaxpayerMemoryModule : IModuleDescriptor
{
    public string Name => "TaxpayerMemory";
    public string Ownership => "Taxpayer identity, fiscal memories and certificate references";
    public string SecurityInvariant => "Never expose raw private keys";
}
