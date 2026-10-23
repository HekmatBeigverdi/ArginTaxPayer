using ArginTaxPayer.Application.Signing;

namespace ArginTaxPayer.Application.Invoices;

/// <summary>
/// Scoped read-only authority authentication for Phase10. This bridge reuses
/// Phase09's protected key custody and authenticated GET_TOKEN/FISCAL_INFORMATION
/// path; it never returns token, key, JWS, packet, raw error, or authorizes dispatch.
/// </summary>
public enum InvoiceAuthorityAuthenticationStatus
{
    Authenticated = 1,
    Denied = 2,
    KeyNotReady = 3,
    CredentialStoreUnavailable = 4,
    AuthorityRejected = 5,
    AuthorityUnavailable = 6,
    TimedOut = 7,
    UnsupportedProfile = 8,
    Indeterminate = 9
}

public sealed record InvoiceAuthorityAuthenticationResult(
    InvoiceAuthorityAuthenticationStatus Status,
    string SafeCode,
    DateTime CheckedAtUtc)
{
    public bool IsAuthenticated => Status == InvoiceAuthorityAuthenticationStatus.Authenticated;
    public bool IsFiscalSubmissionAuthorized => false;
    public bool IsFiscalInvoiceAccepted => false;
}

/// <summary>
/// Must be invoked only after caller's authorization and taxpayer/memory scope
/// are resolved server side. Phase09 adapter repeats and enforces those checks.
/// This contract is not an external customer-facing endpoint.
/// </summary>
public interface IInvoiceAuthorityAuthentication
{
    Task<InvoiceAuthorityAuthenticationResult> CheckAsync(
        string? bearer, Guid taxpayerId, Guid fiscalMemoryId, CancellationToken cancellationToken = default);
}

public sealed class Phase09InvoiceAuthorityAuthentication(
    ISigningReadOnlyConnectionTest phase09Connection) : IInvoiceAuthorityAuthentication
{
    public async Task<InvoiceAuthorityAuthenticationResult> CheckAsync(
        string? bearer, Guid taxpayerId, Guid fiscalMemoryId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(bearer) || taxpayerId == Guid.Empty ||
            fiscalMemoryId == Guid.Empty)
            return Result(InvoiceAuthorityAuthenticationStatus.Denied, DateTime.UtcNow);

        // Never catch cancellation requested by the caller. Phase09's adapter
        // is responsible for enforcing scoped, protected signing and endpoint TLS.
        var authority = await phase09Connection.TestAsync(
            bearer, taxpayerId, fiscalMemoryId, cancellationToken);
        return Result(Map(authority.Status), authority.TestedAtUtc);
    }

    public static InvoiceAuthorityAuthenticationStatus Map(SigningConnectionTestStatus status) =>
        status switch
        {
            SigningConnectionTestStatus.Authenticated => InvoiceAuthorityAuthenticationStatus.Authenticated,
            SigningConnectionTestStatus.NotAuthorized => InvoiceAuthorityAuthenticationStatus.Denied,
            SigningConnectionTestStatus.KeyNotReady => InvoiceAuthorityAuthenticationStatus.KeyNotReady,
            SigningConnectionTestStatus.VaultUnavailable => InvoiceAuthorityAuthenticationStatus.CredentialStoreUnavailable,
            SigningConnectionTestStatus.AuthenticationRejected => InvoiceAuthorityAuthenticationStatus.AuthorityRejected,
            SigningConnectionTestStatus.EndpointUnavailable => InvoiceAuthorityAuthenticationStatus.AuthorityUnavailable,
            SigningConnectionTestStatus.TimedOut => InvoiceAuthorityAuthenticationStatus.TimedOut,
            SigningConnectionTestStatus.UnsupportedProfile => InvoiceAuthorityAuthenticationStatus.UnsupportedProfile,
            _ => InvoiceAuthorityAuthenticationStatus.Indeterminate
        };

    private static InvoiceAuthorityAuthenticationResult Result(
        InvoiceAuthorityAuthenticationStatus status, DateTime checkedAtUtc)
    {
        if (checkedAtUtc.Kind != DateTimeKind.Utc) status = InvoiceAuthorityAuthenticationStatus.Indeterminate;
        var safeCode = status switch
        {
            InvoiceAuthorityAuthenticationStatus.Authenticated => "authority_authenticated",
            InvoiceAuthorityAuthenticationStatus.Denied => "not_authorized",
            InvoiceAuthorityAuthenticationStatus.KeyNotReady => "key_not_ready",
            InvoiceAuthorityAuthenticationStatus.CredentialStoreUnavailable => "credential_store_unavailable",
            InvoiceAuthorityAuthenticationStatus.AuthorityRejected => "authority_authentication_rejected",
            InvoiceAuthorityAuthenticationStatus.AuthorityUnavailable => "authority_unavailable",
            InvoiceAuthorityAuthenticationStatus.TimedOut => "authority_timeout",
            InvoiceAuthorityAuthenticationStatus.UnsupportedProfile => "unsupported_authentication_profile",
            _ => "authority_result_indeterminate"
        };
        return new(status, safeCode,
            checkedAtUtc.Kind == DateTimeKind.Utc ? checkedAtUtc : DateTime.UtcNow);
    }
}
