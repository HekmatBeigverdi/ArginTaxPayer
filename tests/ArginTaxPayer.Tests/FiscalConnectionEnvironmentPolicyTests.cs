using ArginTaxPayer.Domain.Taxpayers;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class FiscalConnectionEnvironmentPolicyTests
{
    [Fact]
    public void Sandbox_credentials_cannot_be_used_on_production()
    {
        var owner=Guid.NewGuid();
        var taxpayer=Guid.NewGuid();
        var memory=Guid.NewGuid();
        var credential=FiscalConnectionCredentialScope.Create(owner,taxpayer,memory,
            FiscalConnectionEnvironment.Sandbox,Guid.NewGuid());
        Assert.True(FiscalConnectionEnvironmentPolicy.CanUse(credential,
            FiscalConnectionEnvironment.Sandbox,owner,taxpayer,memory));
        Assert.False(FiscalConnectionEnvironmentPolicy.CanUse(credential,
            FiscalConnectionEnvironment.Production,owner,taxpayer,memory));
        Assert.False(FiscalConnectionEnvironmentPolicy.CanUse(credential,
            FiscalConnectionEnvironment.Sandbox,Guid.NewGuid(),taxpayer,memory));
    }

    [Fact]
    public void No_environment_fallback_is_allowed()
    {
        foreach (var from in new []{FiscalConnectionEnvironment.Sandbox,
                     FiscalConnectionEnvironment.Production})
        foreach (var to in new []{FiscalConnectionEnvironment.Sandbox,
                     FiscalConnectionEnvironment.Production})
            Assert.False(FiscalConnectionEnvironmentPolicy.AllowsFallback(from,to));
    }

    [Fact]
    public void Sandbox_requires_all_three_independent_preconditions()
    {
        var credential=FiscalConnectionCredentialScope.Create(
            Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),
            FiscalConnectionEnvironment.Sandbox,Guid.NewGuid());
        Assert.False(FiscalConnectionEnvironmentPolicy.IsSandboxReady(credential,
            false,true,true));
        Assert.False(FiscalConnectionEnvironmentPolicy.IsSandboxReady(credential,
            true,false,true));
        Assert.False(FiscalConnectionEnvironmentPolicy.IsSandboxReady(credential,
            true,true,false));
        Assert.True(FiscalConnectionEnvironmentPolicy.IsSandboxReady(credential,
            true,true,true));
    }

    [Fact]
    public void Empty_or_unknown_scope_fails_closed()
    {
        Assert.Throws<ArgumentException>(()=>FiscalConnectionCredentialScope.Create(
            Guid.Empty,Guid.NewGuid(),Guid.NewGuid(),FiscalConnectionEnvironment.Sandbox,
            Guid.NewGuid()));
        Assert.Throws<ArgumentException>(()=>FiscalConnectionCredentialScope.Create(
            Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),(FiscalConnectionEnvironment)33,
            Guid.NewGuid()));
    }
}
