using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class FiscalMemoryAuthorityVerificationTests
{
    [Theory]
    [InlineData("ACTIVE", true)]
    [InlineData("0", true)]
    [InlineData("INACTIVE", false)]
    [InlineData("1", false)]
    [InlineData("UNKNOWN", false)]
    public void Only_authenticated_active_memory_for_the_same_taxpayer_can_activate(string status, bool expected)
    {
        var result = SigningConnectionTestResult.Create(SigningConnectionTestStatus.Authenticated, DateTime.UtcNow)
            with { FiscalInformation = new("Test trade", status, "12345678901", "0012345678") };
        Assert.Equal(expected, FiscalMemoryAuthorityVerificationPolicy.CanActivate(result, "0012345678"));
        Assert.False(FiscalMemoryAuthorityVerificationPolicy.CanActivate(result, "0098765432"));
        Assert.False(FiscalMemoryAuthorityVerificationPolicy.CanActivate(result with { AuthenticationConfirmed = false }, "0012345678"));
        Assert.False(FiscalMemoryAuthorityVerificationPolicy.CanActivate(result with { FiscalInformation = null }, "0012345678"));
    }
}
