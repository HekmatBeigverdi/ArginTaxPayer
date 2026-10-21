namespace ArginTaxPayer.Application.Signing;

/// <summary>
/// Step 22 response boundary. No raw authority response, token, signature, private key,
/// Vault reference, request body or arbitrary exception message may cross this boundary.
/// Authentication success does NOT imply fiscal-memory activation or public-key registration.
/// </summary>
public enum SigningConnectionTestStatus
{
    Authenticated = 1,
    AuthenticationRejected = 2,
    NotAuthorized = 3,
    KeyNotReady = 4,
    VaultUnavailable = 5,
    EndpointUnavailable = 6,
    TimedOut = 7,
    UnsupportedProfile = 8,
    Indeterminate = 9
}

public sealed record SigningConnectionTestResult(
    SigningConnectionTestStatus Status,
    DateTime TestedAtUtc,
    string SafeCode,
    bool AuthenticationConfirmed,
    bool PortalRegistrationConfirmed = false,
    bool FiscalMemoryActivationConfirmed = false)
{
    public static SigningConnectionTestResult Create(
        SigningConnectionTestStatus status, DateTime testedAtUtc)
    {
        if (testedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("UTC timestamp required.", nameof(testedAtUtc));
        return new(status, testedAtUtc, SigningConnectionTestPolicy.Code(status),
            status == SigningConnectionTestStatus.Authenticated);
    }
}

/// <summary>
/// No generic signing or token-returning operation: the authenticated identity, fiscal
/// memory and selected key MUST be resolved and authorized by the server before invocation.
/// </summary>
public interface ISigningReadOnlyConnectionTest
{
    Task<SigningConnectionTestResult> TestAsync(
        string? bearer, Guid taxpayerId, Guid fiscalMemoryId, CancellationToken ct = default);
}

/// <summary>
/// Fail-closed application boundary while the exact officially sourced legacy
/// authentication wire contract and a scoped Vault signer are not yet connected.
/// </summary>
public sealed class UnavailableSigningReadOnlyConnectionTest : ISigningReadOnlyConnectionTest
{
    public Task<SigningConnectionTestResult> TestAsync(
        string? bearer, Guid taxpayerId, Guid fiscalMemoryId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(SigningConnectionTestResult.Create(
            SigningConnectionTestStatus.UnsupportedProfile, DateTime.UtcNow));
    }
}

public static class SigningConnectionTestPolicy
{
    public static string Code(SigningConnectionTestStatus status) => status switch
    {
        SigningConnectionTestStatus.Authenticated => "authority_authenticated",
        SigningConnectionTestStatus.AuthenticationRejected => "authority_authentication_rejected",
        SigningConnectionTestStatus.NotAuthorized => "not_authorized",
        SigningConnectionTestStatus.KeyNotReady => "key_not_ready",
        SigningConnectionTestStatus.VaultUnavailable => "vault_unavailable",
        SigningConnectionTestStatus.EndpointUnavailable => "authority_unavailable",
        SigningConnectionTestStatus.TimedOut => "authority_timeout",
        SigningConnectionTestStatus.UnsupportedProfile => "unsupported_authentication_profile",
        SigningConnectionTestStatus.Indeterminate => "authority_result_indeterminate",
        _ => "authority_result_indeterminate"
    };

    // Never pass through exception.Message, HTTP bodies, status descriptions or
    // unsolicited strings returned by the tax-authority system.
    public static SigningConnectionTestStatus ClassifyHttpStatus(int statusCode) => statusCode switch
    {
        200 => SigningConnectionTestStatus.Indeterminate, // 200 is not proof of a valid token
        401 or 403 => SigningConnectionTestStatus.AuthenticationRejected,
        408 or 504 => SigningConnectionTestStatus.TimedOut,
        429 or 500 or 502 or 503 => SigningConnectionTestStatus.EndpointUnavailable,
        _ => SigningConnectionTestStatus.Indeterminate
    };
}
