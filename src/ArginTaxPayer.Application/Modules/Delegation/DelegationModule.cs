using ArginTaxPayer.Application.Modules;

namespace ArginTaxPayer.Application.Modules.Delegation;

/// <summary>Architectural boundary declaration; contains no implemented business behavior.</summary>
public sealed class DelegationModule : IModuleDescriptor
{
    public string Name => "Delegation";
    public string Ownership => "Workspace assignments and taxpayer grants";
    public string SecurityInvariant => "Scope decisions are enforced server-side";
}
