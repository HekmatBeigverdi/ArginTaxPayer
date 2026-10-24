using ArginTaxPayer.Infrastructure.Signing.Protection;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10SandboxKeyIsolationTests
{
    [Fact]
    public void Sandbox_and_production_bind_different_encryption_aad()
    {
        var owner=Guid.NewGuid();
        var taxpayer=Guid.NewGuid();
        var memory=Guid.NewGuid();
        var key=Guid.NewGuid();
        var operation=Guid.NewGuid();
        var original=new KeyProtectionIdentity(owner,taxpayer,memory,key,operation);
        var sandbox=new KeyProtectionIdentity(owner,taxpayer,memory,key,operation,Sandbox:true);

        Assert.StartsWith("argin:key:v1|",System.Text.Encoding.ASCII.GetString(original.Aad()));
        Assert.StartsWith("argin:key:sandbox:v1|",System.Text.Encoding.ASCII.GetString(sandbox.Aad()));
        Assert.NotEqual(original.Aad(),sandbox.Aad());
        Assert.NotEqual(original.TransitBinding(),sandbox.TransitBinding());

        Assert.Equal($"keys/{owner:N}/{taxpayer:N}/{memory:N}/{operation:N}/{key:N}",
            original.StoragePath());
        Assert.Equal($"keys/sandbox/{owner:N}/{taxpayer:N}/{memory:N}/{operation:N}/{key:N}",
            sandbox.StoragePath());
    }

    [Fact]
    public void No_sandbox_flag_preserves_production_key_identity()
    {
        var identity=new KeyProtectionIdentity(Guid.NewGuid(),Guid.NewGuid(),
            Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid());
        Assert.DoesNotContain("sandbox",identity.StoragePath());
        Assert.Equal(EncodingPrefix(),System.Text.Encoding.ASCII.GetString(identity.Aad())
            .Split('|')[0]);
    }

    private static string EncodingPrefix()=>"argin:key:v1";
}
