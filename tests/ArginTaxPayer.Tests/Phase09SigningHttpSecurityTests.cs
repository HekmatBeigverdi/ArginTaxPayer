using ArginTaxPayer.Application.Signing;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase09SigningHttpSecurityTests
{
    [Fact]
    public void Unauthenticated_request_is_rejected()
    {
        var result = Evaluate(Authenticated: false);
        Assert.Equal(SigningHttpSecurityStatus.Unauthenticated, result.Status);
    }

    [Fact]
    public void Unresolved_scope_is_forbidden()
    {
        var result = Evaluate(ScopeResolved: false);
        Assert.Equal(SigningHttpSecurityStatus.Forbidden, result.Status);
    }

    [Fact]
    public void Mutating_request_requires_valid_csrf_token()
    {
        var result = Evaluate(IsMutating: true, CsrfValid: false);
        Assert.Equal(SigningHttpSecurityStatus.CsrfFailed, result.Status);
    }

    [Fact]
    public void Mutating_request_requires_json_content_type()
    {
        var result = Evaluate(IsMutating: true, ContentType: "text/plain");
        Assert.Equal(SigningHttpSecurityStatus.Invalid, result.Status);
    }

    [Fact]
    public void Request_size_is_bounded()
    {
        var result = Evaluate(ContentLength: SigningHttpSecurityPolicy.MaxRequestBytes + 1);
        Assert.Equal(SigningHttpSecurityStatus.TooLarge, result.Status);
    }

    [Fact]
    public void Rate_limit_is_enforced()
    {
        var result = Evaluate(RequestsInWindow: 10, MaxRequestsPerWindow: 10);
        Assert.Equal(SigningHttpSecurityStatus.RateLimited, result.Status);
    }

    [Fact]
    public void Private_material_requests_are_always_denied()
    {
        var result = Evaluate(RequestsPrivateMaterial: true);
        Assert.Equal(SigningHttpSecurityStatus.Invalid, result.Status);
    }

    [Fact]
    public void Signing_oracle_requests_are_always_denied()
    {
        var result = Evaluate(RequestsSigningOracle: true);
        Assert.Equal(SigningHttpSecurityStatus.Invalid, result.Status);
    }

    [Fact]
    public void Allowed_response_is_no_store()
    {
        var result = Evaluate();
        Assert.Equal(SigningHttpSecurityStatus.Allowed, result.Status);
        Assert.True(result.NoStore);
    }

    [Fact]
    public void Negative_request_values_are_invalid()
    {
        var result = Evaluate(ContentLength: -1);
        Assert.Equal(SigningHttpSecurityStatus.Invalid, result.Status);
    }

    private static SigningHttpSecurityDecision Evaluate(
        bool Authenticated = true,
        bool ScopeResolved = true,
        bool IsMutating = false,
        bool CsrfValid = true,
        long ContentLength = 1024,
        int RequestsInWindow = 0,
        int MaxRequestsPerWindow = 10,
        string ContentType = "application/json",
        bool RequestsPrivateMaterial = false,
        bool RequestsSigningOracle = false) =>
        SigningHttpSecurityPolicy.Evaluate(new(
            Authenticated, ScopeResolved, IsMutating, CsrfValid, ContentLength,
            RequestsInWindow, MaxRequestsPerWindow, ContentType,
            RequestsPrivateMaterial, RequestsSigningOracle));
}
