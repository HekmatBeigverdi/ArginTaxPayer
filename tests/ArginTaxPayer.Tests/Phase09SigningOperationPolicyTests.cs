using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Signing;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase09SigningOperationPolicyTests
{
    [Theory]
    [InlineData(1,2,true)] [InlineData(1,3,false)] [InlineData(1,4,true)]
    [InlineData(1,5,true)] [InlineData(2,3,true)] [InlineData(2,1,false)]
    [InlineData(3,2,false)] [InlineData(4,2,false)] [InlineData(5,2,true)]
    [InlineData(5,3,true)] [InlineData(5,4,true)] [InlineData(5,5,false)]
    public void State_policy_never_restarts_terminal_operations(int from, int to, bool allowed) =>
        Assert.Equal(allowed, SigningOperationPolicy.CanAdvance((short)from, (short)to));

    [Fact]
    public void Authentication_must_be_recent_utc_and_not_in_the_future()
    {
        var now = DateTime.UtcNow;
        Assert.True(SigningOperationPolicy.FreshAuthentication(now.AddMinutes(-5), now));
        Assert.False(SigningOperationPolicy.FreshAuthentication(now.AddMinutes(-5).AddTicks(-1), now));
        Assert.False(SigningOperationPolicy.FreshAuthentication(now.AddTicks(1), now));
        Assert.False(SigningOperationPolicy.FreshAuthentication(DateTime.SpecifyKind(now, DateTimeKind.Local), now));
    }

    [Theory]
    [InlineData(null,false)] [InlineData("",false)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",true)]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",false)]
    public void Public_fingerprint_has_one_canonical_format(string? value, bool expected) =>
        Assert.Equal(expected, SigningOperationPolicy.ValidFingerprint(value));

    [Fact]
    public void Replay_view_contains_no_private_material_or_internal_provider_reference()
    {
        var names = typeof(SigningOperationView).GetProperties().Select(p => p.Name).ToArray();
        Assert.DoesNotContain("CustodyReference", names);
        Assert.DoesNotContain("PrivateKey", names);
        Assert.DoesNotContain("MetadataDigestSha256", names);
        Assert.DoesNotContain("AccessToken", names);
    }
}
