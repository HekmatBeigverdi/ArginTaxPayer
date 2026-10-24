using ArginTaxPayer.Infrastructure.Signing.Connection;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10SandboxAuthorityOriginTests
{
    private static IConfiguration Settings(string origin, bool enabled=true,
        bool verified=true)
    {
        var settings=new Dictionary<string,string?>
        {
            ["SandboxConnectionTest:Enabled"]=enabled?"true":"false",
            ["SandboxConnectionTest:OfficialApiVerified"]=verified?"true":"false",
            ["SandboxConnectionTest:VerifiedApiBaseUrl"]=origin
        };
        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    [Fact]
    public void Production_and_arbitrary_hosts_are_never_allowed()
    {
        foreach(var value in new[]{
            "https://tp.tax.gov.ir/req/api/",
            "https://tax.gov.ir/req/api/",
            "https://sandboxrc.tax.gov.ir.evil.example/req/api/",
            "http://sandboxrc.tax.gov.ir/req/api/",
            "https://sandboxrc.tax.gov.ir:444/req/api/",
            "https://sandboxrc.tax.gov.ir/req/api/../",
            "https://sandboxrc.tax.gov.ir/req/api/?forward=tp.tax.gov.ir",
            "https://name:secret@sandboxrc.tax.gov.ir/req/api/"
        })
            Assert.False(SandboxAuthorityTokenProbe.TryResolveBaseUrl(Settings(value),out _));
    }

    [Fact]
    public void Sandbox_cannot_run_without_explicit_verified_opt_in()
    {
        const string url="https://sandboxrc.tax.gov.ir/req/api/";
        Assert.False(SandboxAuthorityTokenProbe.TryResolveBaseUrl(
            Settings(url,enabled:false),out _));
        Assert.False(SandboxAuthorityTokenProbe.TryResolveBaseUrl(
            Settings(url,verified:false),out _));
        Assert.True(SandboxAuthorityTokenProbe.TryResolveBaseUrl(
            Settings(url),out var uri));
        Assert.Equal("sandboxrc.tax.gov.ir",uri?.Host);
    }
}
