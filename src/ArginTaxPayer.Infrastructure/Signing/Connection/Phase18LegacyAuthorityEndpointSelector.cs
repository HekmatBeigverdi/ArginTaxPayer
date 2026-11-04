using ArginTaxPayer.Domain.Invoices;
using Microsoft.Extensions.Configuration;

namespace ArginTaxPayer.Infrastructure.Signing.Connection;

/// <summary>
/// Pinned, non-configurable endpoint registry for the two legacy SELF_TSP
/// environments. For new operations the environment must be read from
/// immutable operation/memory rows BEFORE calling ResolveOperationEndpoint.
/// Configuration may disable an environment but cannot redirect it.
///
/// This selects an origin; it does NOT authorize invoices, recover signing
/// keys, post packets, or imply that Production legacy protocol is accepted.
/// </summary>
public static class Phase18LegacyAuthorityEndpointSelector
{
    public static readonly Uri SandboxBase = new("https://sandboxrc.tax.gov.ir/req/api/");
    public static readonly Uri ProductionBase = new("https://tp.tax.gov.ir/req/api/");

    public static bool TryResolveOperationEndpoint(
        AuthorityOperationBinding persistedOperation,
        AuthorityOperationBinding persistedMemory,
        TaxAuthorityEnvironment registeredProvider,
        IConfiguration configuration,
        string operation,
        out Uri? endpoint)
    {
        endpoint = null;
        if (!Phase18AuthorityRoutingPolicy.CanUseProvider(
                persistedOperation, persistedMemory, registeredProvider))
            return false;

        if (!TryResolveConfiguredBase(registeredProvider, configuration, out var baseUri) ||
            baseUri is null)
            return false;

        // Restrict paths to the exact protocol operations supported by
        // the existing legacy SDK. Never accept caller-supplied path fragments.
        var path = operation switch
        {
            "GET_TOKEN" => "self-tsp/sync/GET_TOKEN",
            "GET_SERVER_INFORMATION" => "self-tsp/sync/GET_SERVER_INFORMATION",
            "ENQUEUE" => "self-tsp/async/normal-enqueue",
            "INQUIRY_BY_REFERENCE_NUMBER" => "self-tsp/sync/INQUIRY_BY_REFERENCE_NUMBER",
            "INQUIRY_BY_UID" => "self-tsp/sync/INQUIRY_BY_UID",
            _ => null
        };
        if (path is null)
            return false;
        endpoint = new Uri(baseUri, path);
        return true;
    }

    public static bool TryResolveConfiguredBase(
        TaxAuthorityEnvironment environment,
        IConfiguration configuration,
        out Uri? baseUri)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        baseUri = null;
        var (prefix, expected) = environment switch
        {
            TaxAuthorityEnvironment.Sandbox => ("SandboxInvoiceLegacy", SandboxBase),
            TaxAuthorityEnvironment.Production => ("ProductionInvoiceLegacy", ProductionBase),
            _ => (string.Empty, null)
        };
        if (expected is null ||
            configuration[$"{prefix}:Enabled"] != "true" ||
            configuration[$"{prefix}:OperatorApprovedEndpoint"] != "true")
            return false;

        // Independent authorization: Production is never enabled merely by
        // providing a Production URL, and the global application environment
        // must never determine which tax authority is selected.
        if (environment == TaxAuthorityEnvironment.Production &&
            configuration["ArginTaxPayer:EnableOperationalEndpoints"] != "true")
            return false;

        // Exact canonical comparison rejects alternate hosts, ports, credentials,
        // path traversal, query/fragment, Unicode/IDN lookalikes and HTTP.
        var configured = configuration[$"{prefix}:ApiBaseUrl"];
        if (!string.Equals(configured, expected.OriginalString, StringComparison.Ordinal))
            return false;
        baseUri = expected;
        return true;
    }
}
