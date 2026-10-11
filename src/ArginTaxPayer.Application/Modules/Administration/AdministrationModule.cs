using ArginTaxPayer.Application.Modules;

namespace ArginTaxPayer.Application.Modules.Administration;

/// <summary>Architectural boundary declaration; contains no implemented business behavior.</summary>
public sealed class AdministrationModule : IModuleDescriptor
{
    public string Name => "Administration";
    public string Ownership => "Root-level global settings and security administration";
    public string SecurityInvariant => "Delegates never access privileged global logs";
}
