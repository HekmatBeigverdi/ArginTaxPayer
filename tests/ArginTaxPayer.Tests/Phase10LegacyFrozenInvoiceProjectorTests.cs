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
            Subject=InvoiceSubject.Original,
            State=finalized?InvoiceLifecycleState.Finalized:InvoiceLifecycleState.Validated,
            IssueDateUtc=new DateTime(2026,10,25,12,0,0,DateTimeKind.Utc),
            SnapshotJson=JsonSerializer.Serialize(new{
                sourceWorkspaceId=InvoiceId, sourceRevision=1,environment="Sandbox",
                invoiceType=1,pattern=1,lines,buyerPersonType=2,
                settlementMethod=1,cashAmountRial=198,creditAmountRial=0
            })
        };
    }

    [Fact]
    public void Finalized_snapshot_produces_legacy_cash_invoice()
    {
        var invoice=Make();
        var taxId=Phase10FiscalInvoiceIdentifier.Generate("ABC123",26,invoice.IssueDateUtc);
        var projected=Phase10LegacyFrozenInvoiceProjector.Project(invoice,
            taxId,"12345678901","98765432101",
            new Dictionary<Guid,string>{{UnitId,"1"}},"00010-05-AAA");
        using var document=JsonDocument.Parse(projected.Json);
        var header=document.RootElement.GetProperty("header");
        Assert.Equal(198,header.GetProperty("tbill").GetInt64());
        Assert.Equal(1,header.GetProperty("setm").GetInt32());
        Assert.Equal("000000001A",header.GetProperty("inno").GetString());
        Assert.Equal(taxId,header.GetProperty("taxid").GetString());
        Assert.Equal(2,header.GetProperty("tob").GetInt32());
        Assert.Equal(0,header.GetProperty("todam").GetInt64());
        Assert.Equal("1234567890123",document.RootElement.GetProperty("body")[0]
            .GetProperty("sstid").GetString());
    }

    [Theory]
    [InlineData("1234567890")]
    [InlineData("123456789012")]
    [InlineData("1234567890X")]
    public void Seller_identifier_must_match_legacy_economic_identifier_format(string seller)
    {
        Assert.Throws<ArgumentException>(()=>Phase10LegacyFrozenInvoiceProjector.Project(
            Make(),"AAAAAAAAAAAAAAAAAAAAAA",seller,"98765432101",
            new Dictionary<Guid,string>{{UnitId,"1"}}));
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

    [Fact]
    public void Vat_amount_uses_v79_truncation_for_fractional_rial_result()
    {
        var line = new InvoiceEntryLine(Guid.NewGuid(), "خدمات", 3m, 1m, 0m, 18.5m,
            UnitId, OfficialIdentifierSnapshot: "1234567890123");
        var invoice = Make();
        invoice.SnapshotJson = JsonSerializer.Serialize(new {
                sourceWorkspaceId=InvoiceId, sourceRevision=1, environment="Sandbox",
                invoiceType=1, pattern=1, buyerPersonType=2, lines=new[]{line},
                settlementMethod=1, cashAmountRial=3, creditAmountRial=0
            });
        var taxId=Phase10FiscalInvoiceIdentifier.Generate("ABC123",26,invoice.IssueDateUtc);
        var projected=Phase10LegacyFrozenInvoiceProjector.Project(invoice,taxId,
            "12345678901","98765432101",new Dictionary<Guid,string>{{UnitId,"1"}});
        using var document=JsonDocument.Parse(projected.Json);
        var item=document.RootElement.GetProperty("body")[0];
        Assert.Equal(0,item.GetProperty("vam").GetInt64());
        Assert.Equal(3,item.GetProperty("tsstam").GetInt64());
    }

    [Fact]
    public void Missing_buyer_person_type_is_rejected_before_projection()
    {
        var invoice = Make();
        invoice.SnapshotJson = JsonSerializer.Serialize(new {
                sourceWorkspaceId=InvoiceId, sourceRevision=1, environment="Sandbox",
                invoiceType=1, pattern=1, lines=JsonSerializer.Deserialize<InvoiceEntryLine[]>(
                    JsonDocument.Parse(Make().SnapshotJson).RootElement.GetProperty("lines").GetRawText()),
                settlementMethod=1, cashAmountRial=198, creditAmountRial=0
            });
        var taxId=Phase10FiscalInvoiceIdentifier.Generate("ABC123",26,invoice.IssueDateUtc);
        Assert.Throws<ArgumentException>(()=>Phase10LegacyFrozenInvoiceProjector.Project(invoice,taxId,
            "12345678901","98765432101",new Dictionary<Guid,string>{{UnitId,"1"}}));
    }
}
