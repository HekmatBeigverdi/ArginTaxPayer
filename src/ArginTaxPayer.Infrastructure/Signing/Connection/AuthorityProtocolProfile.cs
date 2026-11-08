using ArginTaxPayer.Application.Invoices;
using ArginTaxPayer.Domain.Invoices;

namespace ArginTaxPayer.Infrastructure.Signing.Connection;

public sealed record AuthorityProtocolMetadata(
    string Family, string Version, string WireContract, string SdkEvidence);

/// <summary>
/// The current tested provider profile. Future SDK generations must be added as
/// separate implementations and contract-tested before they can be selected.
/// </summary>
public sealed class AuthorityProtocolProfile : IAuthorityInvoiceProvider
{
    public const string LegacyFamily = "legacy-self-tsp";
    public const string LegacyVersion = "0.0.x";

    public string Family => LegacyFamily;
    public string Version => LegacyVersion;
    public string WireContract => "SELF_TSP legacy";
    public string SdkEvidence => "source-0.0.30;package-0.0.34";

    public AuthorityProtocolMetadata ToMetadata() =>
        new(Family, Version, WireContract, SdkEvidence);

    public static bool CanUseLegacyRoute(IAuthorityInvoiceProvider provider,
        TaxAuthorityEnvironment environment) =>
        provider.Supports(environment) &&
        string.Equals(provider.Family, LegacyFamily, StringComparison.Ordinal) &&
        string.Equals(provider.Version, LegacyVersion, StringComparison.Ordinal) &&
        string.Equals(provider.WireContract, "SELF_TSP legacy", StringComparison.Ordinal);

    public bool Supports(TaxAuthorityEnvironment environment) =>
        environment is TaxAuthorityEnvironment.Production or TaxAuthorityEnvironment.Sandbox;

    public static bool TryResolve(string? family, string? version,
        out AuthorityProtocolProfile? profile)
    {
        profile = null;
        if (!string.Equals(family, LegacyFamily, StringComparison.Ordinal) ||
            !string.Equals(version, LegacyVersion, StringComparison.Ordinal))
            return false;

        profile = new AuthorityProtocolProfile();
        return true;
    }
}
