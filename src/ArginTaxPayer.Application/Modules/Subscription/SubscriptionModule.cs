using ArginTaxPayer.Application.Modules;

namespace ArginTaxPayer.Application.Modules.Subscription;

/// <summary>Architectural boundary declaration; contains no implemented business behavior.</summary>
public sealed class SubscriptionModule : IModuleDescriptor
{
    public string Name => "Subscription";
    public string Ownership => "Subscription orders and entitlement usage";
    public string SecurityInvariant => "Never activate from unverified callback";
}
