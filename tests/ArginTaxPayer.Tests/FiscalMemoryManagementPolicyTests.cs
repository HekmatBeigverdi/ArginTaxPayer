using ArginTaxPayer.Domain.Taxpayers;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class FiscalMemoryManagementPolicyTests
{
    [Theory]
    [InlineData("abc123", "ABC123")]
    [InlineData(" ABC123 ", "ABC123")]
    public void Accepts_and_normalizes_authority_identifiers_as_unverified_metadata(string input, string normalized)
    {
        Assert.True(FiscalMemoryOnboardingPolicy.TryNormalizeIdentifier(input, out var actual));
        Assert.Equal(normalized, actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc-123")]
    [InlineData("abc 123")]
    [InlineData("abc/123")]
    public void Rejects_unsafe_identifier_shapes(string input)
        => Assert.False(FiscalMemoryOnboardingPolicy.TryNormalizeIdentifier(input, out _));

    [Fact]
    public void Only_pending_to_retired_is_allowed_without_a_subscription_binding_guard()
    {
        Assert.True(FiscalMemoryOnboardingPolicy.CanLocallyRetireNeverActivatedMemory(
            FiscalMemoryStatus.PendingVerification, FiscalMemoryStatus.Retired));
        Assert.False(FiscalMemoryOnboardingPolicy.CanLocallyRetireNeverActivatedMemory(
            FiscalMemoryStatus.PendingVerification, FiscalMemoryStatus.Active));
        Assert.False(FiscalMemoryOnboardingPolicy.CanLocallyRetireNeverActivatedMemory(
            FiscalMemoryStatus.Active, FiscalMemoryStatus.Retired));
        Assert.False(FiscalMemoryOnboardingPolicy.CanLocallyRetireNeverActivatedMemory(
            FiscalMemoryStatus.Suspended, FiscalMemoryStatus.Active));
    }
}
