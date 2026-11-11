using System.Security.Cryptography;
using ArginTaxPayer.Infrastructure.Persistence.Signing;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class SandboxSameKeyRecoveryTests
{
    [Fact]
    public void Matching_public_key_can_be_restored_but_another_RSA_key_is_not_a_recovery()
    {
        using var original = RSA.Create(2048); using var other = RSA.Create(2048);
        var registered = original.ExportSubjectPublicKeyInfo();
        Assert.Null(SandboxSameKeyRecoveryPolicy.Validate(true, true, true, 3, 3, registered, original.ExportSubjectPublicKeyInfo()));
        Assert.Equal("sandbox_key_pair_mismatch", SandboxSameKeyRecoveryPolicy.Validate(true, true, true, 3, 3, registered, other.ExportSubjectPublicKeyInfo()));
    }
    [Theory]
    [InlineData(false,true,true,3,3,"sandbox_owner_reauthentication_required")]
    [InlineData(true,false,true,3,3,"sandbox_owner_reauthentication_required")]
    [InlineData(true,true,false,3,3,"sandbox_custody_consent_required")]
    [InlineData(true,true,true,2,3,"sandbox_memory_changed")]
    public void Recovery_requires_owner_fresh_authentication_consent_and_current_revision(bool owner,bool fresh,bool consent,long expected,long actual,string error)
    {
        Assert.Equal(error,SandboxSameKeyRecoveryPolicy.Validate(owner,fresh,consent,expected,actual,[1,2,3],[1,2,3]));
    }
}
