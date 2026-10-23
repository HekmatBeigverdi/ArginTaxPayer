using ArginTaxPayer.Application.Invoices;
using ArginTaxPayer.Application.Signing;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10AuthorityAuthenticationTests
{
    private static readonly DateTime At = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);

    private sealed class FakeConnection(SigningConnectionTestStatus outcome) : ISigningReadOnlyConnectionTest
    {
        public int Calls { get; private set; }
        public Task<SigningConnectionTestResult> TestAsync(
            string? bearer, Guid taxpayerId, Guid fiscalMemoryId,
            CancellationToken ct = default)
        {
            Calls++;
            Assert.Equal("synthetic-auth-session", bearer);
            Assert.NotEqual(Guid.Empty, taxpayerId);
            Assert.NotEqual(Guid.Empty, fiscalMemoryId);
            return Task.FromResult(SigningConnectionTestResult.Create(outcome, At));
        }
    }

    [Fact]
    public async Task Phase09_authenticated_read_only_response_is_not_invoice_permission()
    {
        var fake = new FakeConnection(SigningConnectionTestStatus.Authenticated);
        var sut = new Phase09InvoiceAuthorityAuthentication(fake);
        var result = await sut.CheckAsync("synthetic-auth-session", Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(InvoiceAuthorityAuthenticationStatus.Authenticated, result.Status);
        Assert.Equal("authority_authenticated", result.SafeCode);
        Assert.Equal(At, result.CheckedAtUtc);
        Assert.True(result.IsAuthenticated);
        Assert.False(result.IsFiscalSubmissionAuthorized);
        Assert.False(result.IsFiscalInvoiceAccepted);
        Assert.Equal(1, fake.Calls);
    }

    [Theory]
    [InlineData(SigningConnectionTestStatus.AuthenticationRejected, InvoiceAuthorityAuthenticationStatus.AuthorityRejected)]
    [InlineData(SigningConnectionTestStatus.NotAuthorized, InvoiceAuthorityAuthenticationStatus.Denied)]
    [InlineData(SigningConnectionTestStatus.KeyNotReady, InvoiceAuthorityAuthenticationStatus.KeyNotReady)]
    [InlineData(SigningConnectionTestStatus.VaultUnavailable, InvoiceAuthorityAuthenticationStatus.CredentialStoreUnavailable)]
    [InlineData(SigningConnectionTestStatus.EndpointUnavailable, InvoiceAuthorityAuthenticationStatus.AuthorityUnavailable)]
    [InlineData(SigningConnectionTestStatus.TimedOut, InvoiceAuthorityAuthenticationStatus.TimedOut)]
    [InlineData(SigningConnectionTestStatus.UnsupportedProfile, InvoiceAuthorityAuthenticationStatus.UnsupportedProfile)]
    [InlineData(SigningConnectionTestStatus.Indeterminate, InvoiceAuthorityAuthenticationStatus.Indeterminate)]
    public async Task Result_map_preserves_safe_non_success_distinctions(
        SigningConnectionTestStatus source, InvoiceAuthorityAuthenticationStatus expected)
    {
        var sut = new Phase09InvoiceAuthorityAuthentication(new FakeConnection(source));
        var result = await sut.CheckAsync("synthetic-auth-session", Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(expected, result.Status);
        Assert.False(result.IsAuthenticated);
        Assert.False(result.IsFiscalSubmissionAuthorized);
    }

    [Fact]
    public async Task Missing_auth_or_scope_fails_without_calling_authority()
    {
        var fake = new FakeConnection(SigningConnectionTestStatus.Authenticated);
        var sut = new Phase09InvoiceAuthorityAuthentication(fake);
        var r1 = await sut.CheckAsync(null, Guid.NewGuid(), Guid.NewGuid());
        var r2 = await sut.CheckAsync("synthetic-auth-session", Guid.Empty, Guid.NewGuid());
        var r3 = await sut.CheckAsync("synthetic-auth-session", Guid.NewGuid(), Guid.Empty);
        Assert.Equal(InvoiceAuthorityAuthenticationStatus.Denied, r1.Status);
        Assert.Equal(InvoiceAuthorityAuthenticationStatus.Denied, r2.Status);
        Assert.Equal(InvoiceAuthorityAuthenticationStatus.Denied, r3.Status);
        Assert.Equal(0, fake.Calls);
    }

    [Fact]
    public void Unknown_status_fails_closed()
    {
        Assert.Equal(InvoiceAuthorityAuthenticationStatus.Indeterminate,
            Phase09InvoiceAuthorityAuthentication.Map((SigningConnectionTestStatus)999));
    }

    [Fact]
    public async Task Cancellation_propagates_without_network_call()
    {
        var fake = new FakeConnection(SigningConnectionTestStatus.Authenticated);
        var sut = new Phase09InvoiceAuthorityAuthentication(fake);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.CheckAsync("synthetic-auth-session", Guid.NewGuid(), Guid.NewGuid(), cts.Token));
        Assert.Equal(0, fake.Calls);
    }
}
