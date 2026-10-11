using ArginTaxPayer.Application.Modules;

namespace ArginTaxPayer.Application.Modules.FiscalRules;

/// <summary>Architectural boundary declaration; contains no implemented business behavior.</summary>
public sealed class FiscalRulesModule : IModuleDescriptor
{
    public string Name => "FiscalRules";
    public string Ownership => "Versioned invoice validators and calculation policies";
    public string SecurityInvariant => "No outbound network or payment dependencies";
}
