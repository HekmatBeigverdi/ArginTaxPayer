namespace ArginTaxPayer.Application.Abstractions.Tenancy;

/// <summary>
/// A scoped, pre-authorized taxpayer selection. This contract does not itself
/// authenticate an actor or grant access to any taxpayer.
/// </summary>
public interface ITaxpayerScope
{
    bool IsInitialized { get; }
    Guid RequireTaxpayerId();
}

/// <summary>
/// Called only by a trusted entry-point after its authorization check.
/// Never populate a scope directly from an unverified request header.
/// </summary>
public interface ITaxpayerScopeInitializer
{
    void Initialize(Guid taxpayerId);
}
