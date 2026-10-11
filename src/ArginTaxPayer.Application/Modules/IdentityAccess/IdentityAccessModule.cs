using ArginTaxPayer.Application.Modules;

namespace ArginTaxPayer.Application.Modules.IdentityAccess;

/// <summary>Architectural boundary declaration; contains no implemented business behavior.</summary>
public sealed class IdentityAccessModule : IModuleDescriptor
{
    public string Name => "IdentityAccess";
    public string Ownership => "Users, roles, sessions and root security";
    public string SecurityInvariant => "Global security audit remains Root-only";
}
