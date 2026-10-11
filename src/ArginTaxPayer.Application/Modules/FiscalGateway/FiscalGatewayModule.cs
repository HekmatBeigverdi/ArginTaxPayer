using ArginTaxPayer.Application.Modules;

namespace ArginTaxPayer.Application.Modules.FiscalGateway;

/// <summary>Architectural boundary declaration; contains no implemented business behavior.</summary>
public sealed class FiscalGatewayModule : IModuleDescriptor
{
    public string Name => "FiscalGateway";
    public string Ownership => "Tax authority adapters and submission state";
    public string SecurityInvariant => "Must not mutate commercial receipts";
}
