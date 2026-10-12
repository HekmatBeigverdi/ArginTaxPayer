using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class IdentityCredentialTests
{
    [Fact]
    public void PhaseOneBootstrapPasswordHashCanBeVerified()
    {
        const string password = "Correct-Horse-Lamp-2026!";
        var legacyHash = InitialAdministratorBootstrap.HashPassword(password);
        Assert.True(IdentityPasswordHasher.Verify(password, legacyHash));
        Assert.False(IdentityPasswordHasher.Verify("NotThePassword!", legacyHash));
        Assert.False(IdentityPasswordHasher.Verify(password, "malformed"));
    }

    [Fact]
    public void HashesUseDifferentRandomSalts()
    {
        const string password = "An-Appropriate-Demo-Password-2026!";
        var first = IdentityPasswordHasher.Hash(password);
        var second = IdentityPasswordHasher.Hash(password);
        Assert.NotEqual(first, second);
        Assert.True(IdentityPasswordHasher.Verify(password, first));
        Assert.True(IdentityPasswordHasher.Verify(password, second));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("")]
    public void WeakLengthPasswordIsRejected(string password)
        => Assert.Throws<ArgumentException>(() => IdentityPasswordHasher.Validate(password));
}
