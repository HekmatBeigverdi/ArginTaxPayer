using ArginTaxPayer.Application.Modules;

namespace ArginTaxPayer.Application.Modules.Collections;

/// <summary>Architectural boundary declaration; contains no implemented business behavior.</summary>
public sealed class CollectionsModule : IModuleDescriptor
{
    public string Name => "Collections";
    public string Ownership => "Verified receipts and payment allocations";
    public string SecurityInvariant => "Never conflate receipt with partner settlement";
}
