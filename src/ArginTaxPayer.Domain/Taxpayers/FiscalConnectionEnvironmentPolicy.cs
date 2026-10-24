namespace ArginTaxPayer.Domain.Taxpayers;

/// <summary>
/// Environment is part of the immutable fiscal credential identity.
/// Never infer environment from caller-supplied URL or reuse production
/// fiscal-memory credentials in Sandbox, even on timeout/failure.
/// </summary>
public enum FiscalConnectionEnvironment
{
    Production = 1,
    Sandbox = 2
}

public sealed record FiscalConnectionCredentialScope(
    Guid CustomerUserId, Guid TaxpayerId, Guid FiscalMemoryId,
    FiscalConnectionEnvironment Environment, Guid SigningKeyVersionId)
{
    public static FiscalConnectionCredentialScope Create(
        Guid customerUserId, Guid taxpayerId, Guid fiscalMemoryId,
        FiscalConnectionEnvironment environment, Guid signingKeyVersionId)
    {
        if (customerUserId == Guid.Empty || taxpayerId == Guid.Empty ||
            fiscalMemoryId == Guid.Empty || signingKeyVersionId == Guid.Empty ||
            !Enum.IsDefined(environment))
            throw new ArgumentException("Invalid fiscal credential environment scope.");
        return new(customerUserId, taxpayerId, fiscalMemoryId, environment, signingKeyVersionId);
    }
}

/// <summary>
/// Pure isolation policy. Does not activate a Sandbox provider.
/// Concrete endpoint origin must be pinned by trusted server configuration,
/// individually verified against official published API documentation.
/// </summary>
public static class FiscalConnectionEnvironmentPolicy
{
    public static bool CanUse(
        FiscalConnectionCredentialScope credential,
        FiscalConnectionEnvironment requestedEnvironment,
        Guid customerUserId, Guid taxpayerId, Guid fiscalMemoryId) =>
        credential.CustomerUserId == customerUserId &&
        credential.TaxpayerId == taxpayerId &&
        credential.FiscalMemoryId == fiscalMemoryId &&
        credential.Environment == requestedEnvironment &&
        Enum.IsDefined(requestedEnvironment);

    public static bool AllowsFallback(
        FiscalConnectionEnvironment failed, FiscalConnectionEnvironment fallback) => false;

    public static bool IsSandboxReady(
        FiscalConnectionCredentialScope credential,
        bool verifiedSandboxApiOrigin,
        bool verifiedSandboxKeyCustody,
        bool verifiedSandboxMemoryRegistration) =>
        credential.Environment == FiscalConnectionEnvironment.Sandbox &&
        verifiedSandboxApiOrigin &&
        verifiedSandboxKeyCustody &&
        verifiedSandboxMemoryRegistration;
}
