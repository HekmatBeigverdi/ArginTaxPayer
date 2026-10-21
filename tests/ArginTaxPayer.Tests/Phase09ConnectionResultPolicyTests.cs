using ArginTaxPayer.Application.Signing;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase09ConnectionResultPolicyTests
{
    [Theory]
    [InlineData(200, SigningConnectionTestStatus.Indeterminate)]
    [InlineData(401, SigningConnectionTestStatus.AuthenticationRejected)]
    [InlineData(403, SigningConnectionTestStatus.AuthenticationRejected)]
    [InlineData(408, SigningConnectionTestStatus.TimedOut)]
    [InlineData(429, SigningConnectionTestStatus.EndpointUnavailable)]
    [InlineData(500, SigningConnectionTestStatus.EndpointUnavailable)]
    [InlineData(502, SigningConnectionTestStatus.EndpointUnavailable)]
    [InlineData(503, SigningConnectionTestStatus.EndpointUnavailable)]
    [InlineData(504, SigningConnectionTestStatus.TimedOut)]
    [InlineData(302, SigningConnectionTestStatus.Indeterminate)]
    public void Http_status_alone_never_claims_authenticated(int status, SigningConnectionTestStatus expected)
    {
        var classified = SigningConnectionTestPolicy.ClassifyHttpStatus(status);
        Assert.Equal(expected, classified);
        Assert.NotEqual(SigningConnectionTestStatus.Authenticated, classified);
    }

    [Fact]
    public void Result_explicitly_distinguishes_authentication_from_registration_and_activation()
    {
        var result = SigningConnectionTestResult.Create(
            SigningConnectionTestStatus.Authenticated, DateTime.UtcNow);
        Assert.True(result.AuthenticationConfirmed);
        Assert.False(result.PortalRegistrationConfirmed);
        Assert.False(result.FiscalMemoryActivationConfirmed);
        Assert.Equal("authority_authenticated", result.SafeCode);
    }

    [Fact]
    public void Result_refuses_non_utc_time()
    {
        Assert.Throws<ArgumentException>(() => SigningConnectionTestResult.Create(
            SigningConnectionTestStatus.Indeterminate, DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Local)));
    }

    [Fact]
    public async Task Unwired_authority_adapter_fails_closed()
    {
        ISigningReadOnlyConnectionTest service = new UnavailableSigningReadOnlyConnectionTest();
        var result = await service.TestAsync(null, Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(SigningConnectionTestStatus.UnsupportedProfile, result.Status);
        Assert.False(result.AuthenticationConfirmed);
        Assert.Equal("unsupported_authentication_profile", result.SafeCode);
    }

    [Theory]
    [InlineData(SigningConnectionTestStatus.NotAuthorized, "not_authorized")]
    [InlineData(SigningConnectionTestStatus.VaultUnavailable, "vault_unavailable")]
    [InlineData(SigningConnectionTestStatus.KeyNotReady, "key_not_ready")]
    [InlineData(SigningConnectionTestStatus.TimedOut, "authority_timeout")]
    public void Public_codes_are_fixed_and_safe(SigningConnectionTestStatus status, string code)
    {
        var result = SigningConnectionTestResult.Create(status, DateTime.UtcNow);
        Assert.Equal(code, result.SafeCode);
        Assert.False(result.AuthenticationConfirmed);
    }
}
