using System.Text.Json;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10LegacyFrozenInvoiceProjectorTests
{
    private static readonly Guid InvoiceId=Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UnitId=Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static StoredInvoice Make(bool finalized=true,decimal unitPrice=100)
    {
        var line=new InvoiceEntryLine(Guid.NewGuid(),"خدمات",2,unitPrice,10,10m,
            UnitId,OfficialIdentifierSnapshot:"1234567890123");
        var lines=new[]{line};
        return new StoredInvoice{
            Id=InvoiceId,Revision=1,
            State=finalized?InvoiceLifecycleState.Finalized:InvoiceLifecycleState.Validated,
            IssueDateUtc=new DateTime(2026,10,25,12,0,0,DateTimeKind.Utc),
            SnapshotJson=JsonSerializer.Serialize(new{
                sourceWorkspaceId=InvoiceId, sourceRevision=1,environment="Sandbox",
                invoiceType=1,pattern=1,lines,
                settlementMethod=1,cashAmountRial=198,creditAmountRial=0
            })
        };
    }

    [Fact]
    public void Finalized_snapshot_produces_legacy_cash_invoice()
    {
        var projected=Phase10LegacyFrozenInvoiceProjector.Project(Make(),
            "AAAAAAAAAAAAAAAAAAAAAA","12345678901","98765432101",
            new Dictionary<Guid,string>{{UnitId,"1"}},"00010-05-AAA");
        using var document=JsonDocument.Parse(projected.Json);
        var header=document.RootElement.GetProperty("header");
        Assert.Equal(198,header.GetProperty("tbill").GetInt64());
        Assert.Equal(1,header.GetProperty("setm").GetInt32());
        Assert.Equal("00010-05-AAA",header.GetProperty("inno").GetString());
        Assert.Equal("1234567890123",document.RootElement.GetProperty("body")[0]
            .GetProperty("sstid").GetString());
    }

    [Fact]
    public void Draft_or_unresolved_unit_cannot_project()
    {
        Assert.Throws<ArgumentException>(()=>Phase10LegacyFrozenInvoiceProjector.Project(
            Make(false),"AAAAAAAAAAAAAAAAAAAAAA","12345678901","98765432101",
            new Dictionary<Guid,string>{{UnitId,"1"}}));
        Assert.Throws<ArgumentException>(()=>Phase10LegacyFrozenInvoiceProjector.Project(
            Make(),"AAAAAAAAAAAAAAAAAAAAAA","12345678901","98765432101",
            new Dictionary<Guid,string>()));
    }

    [Fact]
    public void Fractional_rial_amounts_are_not_silently_rounded()
    {
        Assert.Throws<ArgumentException>(()=>Phase10LegacyFrozenInvoiceProjector.Project(
            Make(unitPrice:100.25m),"AAAAAAAAAAAAAAAAAAAAAA",
            "12345678901","98765432101",
            new Dictionary<Guid,string>{{UnitId,"1"}}));
    }
}
