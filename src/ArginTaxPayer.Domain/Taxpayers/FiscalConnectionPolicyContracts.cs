namespace ArginTaxPayer.Domain.Taxpayers;

/// <summary>
/// Cross-phase contract for the annual, unlimited-invoice, single-taxpayer entitlement.
/// Enforcement and authoritative subscription state are owned by Phase 12.
/// </summary>
public enum AnnualTaxpayerBindingStatus
{
    Unbound = 1,
    LockedAfterSuccessfulAuthorityConnection = 2,
    ResetApprovedPendingRebind = 3
}

public enum FiscalBindingReviewStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Applied = 4
}

public enum FiscalBindingReviewReason
{
    FiscalCaseDormant = 1,
    FiscalMemoryReplaced = 2,
    CertificateOrKeyChanged = 3,
    AuthorityRegistrationChanged = 4,
    Other = 5
}

public enum FiscalBindingChangeScope
{
    ReplaceMemorySameTaxpayer = 1,
    ReplaceTaxpayerAndMemory = 2
}

public enum TaxAuthorityConnectivityResult
{
    Success = 1,
    InvalidCredentialsOrToken = 2,
    SignatureOrKeyMismatch = 3,
    AuthorityTimeout = 4,
    AuthorityServerError = 5,
    NetworkOrDnsFailure = 6,
    Inconclusive = 7
}

public enum FiscalKeyOnboardingMode
{
    GenerateNewKeyPair = 1,
    ImportExistingPrivateKey = 2
}

/// <summary>
/// Audit-friendly approval information. An approval never destroys the previous
/// taxpayer/memory link or any submitted invoice references.
/// </summary>
public sealed record FiscalBindingReviewContract(
    Guid RequestId,
    Guid SubscriptionId,
    Guid CurrentTaxpayerId,
    Guid? CurrentFiscalMemoryId,
    FiscalBindingChangeScope Scope,
    FiscalBindingReviewReason Reason,
    string Explanation,
    Guid RequestedByUserId,
    DateTimeOffset RequestedAtUtc,
    FiscalBindingReviewStatus Status,
    Guid? ReviewedByLevel1UserId,
    DateTimeOffset? ReviewedAtUtc,
    string? DecisionReason);

/// <summary>
/// Diagnostic outcome, not proof of a successful invoice submission.
/// Contains no access token, private key, raw signed request or secrets.
/// </summary>
public sealed record TaxAuthorityConnectivityCheckContract(
    Guid CheckId,
    Guid TaxpayerId,
    Guid FiscalMemoryId,
    TaxAuthorityConnectivityResult Result,
    DateTimeOffset CheckedAtUtc,
    string? SafeCorrelationId,
    int? UpstreamStatusCode);

/// <summary>
/// Public metadata only. Private key import and generation happen within a
/// protected server-side custody boundary implemented in Phase 09.
/// </summary>
public sealed record FiscalKeyOnboardingContract(
    Guid TaxpayerId,
    Guid FiscalMemoryId,
    FiscalKeyOnboardingMode Mode,
    string? ExistingPublicKeyFingerprintSha256,
    string? CustodyReference);

public static class AnnualTaxpayerBindingPolicy
{
    /// <summary>Only a verified positive authority connection can establish the annual lock.</summary>
    public static bool ShouldLock(TaxAuthorityConnectivityResult result) =>
        result == TaxAuthorityConnectivityResult.Success;

    /// <summary>
    /// Ordinary customer changes after lock must always fail closed.
    /// Approval is a separate audited Level 1 workflow, never this predicate.
    /// </summary>
    public static bool CanCustomerChangeTaxpayer(
        AnnualTaxpayerBindingStatus status) =>
        status == AnnualTaxpayerBindingStatus.Unbound;

    /// <summary>
    /// A Level 2 delegate may request an exception only within current authorized scope;
    /// the actual authorization must be evaluated from the database by the server.
    /// </summary>
    public static bool RequiresLevel1ManualApproval(
        FiscalBindingChangeScope scope) =>
        scope is FiscalBindingChangeScope.ReplaceMemorySameTaxpayer
              or FiscalBindingChangeScope.ReplaceTaxpayerAndMemory;
}
