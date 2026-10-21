namespace ArginTaxPayer.Application.Signing;

public enum SigningHttpSecurityStatus { Allowed, Unauthenticated, Forbidden, CsrfFailed, TooLarge, RateLimited, Invalid }

public sealed record SigningHttpSecurityInput(
    bool Authenticated,
    bool ScopeResolved,
    bool IsMutating,
    bool CsrfValid,
    long ContentLength,
    int RequestsInWindow,
    int MaxRequestsPerWindow,
    string? ContentType,
    bool RequestsPrivateMaterial,
    bool RequestsSigningOracle);

public sealed record SigningHttpSecurityDecision(SigningHttpSecurityStatus Status, bool NoStore, string? ErrorCode);

public static class SigningHttpSecurityPolicy
{
    public const long MaxRequestBytes = 256 * 1024;

    public static SigningHttpSecurityDecision Evaluate(SigningHttpSecurityInput input)
    {
        if (input is null || input.ContentLength < 0 || input.MaxRequestsPerWindow < 1 ||
            input.RequestsInWindow < 0 || input.RequestsPrivateMaterial || input.RequestsSigningOracle)
            return Deny(SigningHttpSecurityStatus.Invalid, "invalid_signing_request");
        if (!input.Authenticated) return Deny(SigningHttpSecurityStatus.Unauthenticated, "authentication_required");
        if (!input.ScopeResolved) return Deny(SigningHttpSecurityStatus.Forbidden, "scope_denied");
        if (input.ContentLength > MaxRequestBytes) return Deny(SigningHttpSecurityStatus.TooLarge, "request_too_large");
        if (input.RequestsInWindow >= input.MaxRequestsPerWindow)
            return Deny(SigningHttpSecurityStatus.RateLimited, "rate_limited");
        if (input.IsMutating && !input.CsrfValid)
            return Deny(SigningHttpSecurityStatus.CsrfFailed, "csrf_failed");
        if (input.IsMutating && !string.Equals(input.ContentType, "application/json", StringComparison.OrdinalIgnoreCase))
            return Deny(SigningHttpSecurityStatus.Invalid, "json_required");
        return new(SigningHttpSecurityStatus.Allowed, NoStore: true, ErrorCode: null);
    }

    private static SigningHttpSecurityDecision Deny(SigningHttpSecurityStatus status, string error) =>
        new(status, NoStore: true, error);
}
