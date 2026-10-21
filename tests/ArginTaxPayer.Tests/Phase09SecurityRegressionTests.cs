using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Signing;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase09SecurityRegressionTests
{
    [Fact] public void Scope_denial_is_not_authenticated_as_not_found() => Assert.Equal(SigningHttpSecurityStatus.Forbidden, Evaluate(scope: false).Status);
    [Fact] public void Missing_csrf_blocks_mutation() => Assert.Equal(SigningHttpSecurityStatus.CsrfFailed, Evaluate(mutation: true, csrf: false).Status);
    [Fact] public void Private_material_is_never_an_allowed_response() => Assert.NotEqual(SigningHttpSecurityStatus.Allowed, Evaluate(privateMaterial: true).Status);
    [Fact] public void Generic_signing_oracle_is_never_an_allowed_response() => Assert.NotEqual(SigningHttpSecurityStatus.Allowed, Evaluate(oracle: true).Status);
    [Fact] public void Oversized_payload_is_rejected_before_scope_work() => Assert.Equal(SigningHttpSecurityStatus.TooLarge, Evaluate(bytes: SigningHttpSecurityPolicy.MaxRequestBytes + 1).Status);
    [Fact] public void Rate_limit_is_rejected_at_the_boundary() => Assert.Equal(SigningHttpSecurityStatus.RateLimited, Evaluate(requests: 5, limit: 5).Status);
    [Fact] public void Compromised_key_cannot_sign() => Assert.False(SigningKeyLifecyclePolicy.CanSign(SigningKeyLifecyclePolicy.Compromised));
    [Fact] public void Superseded_key_cannot_sign() => Assert.False(SigningKeyLifecyclePolicy.CanSign(SigningKeyLifecyclePolicy.Superseded));
    [Fact] public void Terminal_lifecycle_cannot_resume() => Assert.False(SigningKeyLifecyclePolicy.CanTransition(SigningKeyLifecyclePolicy.Compromised, SigningKeyLifecyclePolicy.LocallyReady));
    [Fact] public void Review_approval_expires_instead_of_becoming_permanent()
    {
        var reviewedAtUtc = new DateTime(2026, 10, 21, 9, 0, 0, DateTimeKind.Utc);
        Assert.Equal(reviewedAtUtc.AddHours(24), SigningChangeReviewPolicy.Expiry(reviewedAtUtc));
    }

    private static SigningHttpSecurityDecision Evaluate(bool scope = true, bool mutation = false, bool csrf = true,
        long bytes = 1024, int requests = 0, int limit = 10, bool privateMaterial = false, bool oracle = false) =>
        SigningHttpSecurityPolicy.Evaluate(new(true, scope, mutation, csrf, bytes, requests, limit,
            "application/json", privateMaterial, oracle));
}
