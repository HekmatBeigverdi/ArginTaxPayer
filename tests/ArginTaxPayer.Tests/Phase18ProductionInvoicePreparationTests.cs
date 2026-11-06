using System.Text.Json;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase18ProductionInvoicePreparationTests
{
    private static readonly Guid Memory = Guid.NewGuid();
    private static readonly Guid Unit = Guid.NewGuid();

    [Fact]
    public void Explicit_owned_production_binding_projects_original_invoice()
    {
        var invoice = Invoice();
        var binding = Binding(invoice);
        var taxId = Phase10FiscalInvoiceIdentifier.Generate("ABC123", 42, invoice.IssueDateUtc);
        var wire = Phase10LegacyFrozenInvoiceProjector.Project(invoice, taxId,
            "12345678901", "98765432101", new Dictionary<Guid,string>{{Unit,"1"}},
            validatedAuthorityBinding: binding);
        using var parsed = JsonDocument.Parse(wire.Json);
        Assert.Equal(taxId, parsed.RootElement.GetProperty("header").GetProperty("taxid").GetString());
        Assert.Equal("000000002A", parsed.RootElement.GetProperty("header").GetProperty("inno").GetString());
    }

    [Fact]
    public void Explicit_owned_classified_sandbox_binding_projects_to_sandbox()
    {
        var invoice=Invoice();
        invoice.SnapshotJson=invoice.SnapshotJson.Replace("Production","Sandbox");
        var binding=Binding(invoice) with { Environment=TaxAuthorityEnvironment.Sandbox };
        var wire=Phase10LegacyFrozenInvoiceProjector.Project(invoice,
            Phase10FiscalInvoiceIdentifier.Generate("ABC123",42,invoice.IssueDateUtc),
            "12345678901","98765432101",new Dictionary<Guid,string>{{Unit,"1"}},
            validatedAuthorityBinding:binding);
        using var parsed=JsonDocument.Parse(wire.Json);
        Assert.Equal("000000002A",parsed.RootElement.GetProperty("header").GetProperty("inno").GetString());
    }

    [Fact]
    public void Production_json_alone_never_enables_projector()
    {
        var invoice = Invoice();
        Assert.Throws<NotSupportedException>(() => Phase10LegacyFrozenInvoiceProjector.Project(
            invoice, Phase10FiscalInvoiceIdentifier.Generate("ABC123",42,invoice.IssueDateUtc),
            "12345678901","98765432101",new Dictionary<Guid,string>{{Unit,"1"}}));
    }

    [Fact]
    public void Wrong_owner_memory_or_environment_binding_is_rejected()
    {
        var invoice = Invoice();
        var binding = Binding(invoice);
        foreach(var invalid in new[] { binding with { CustomerUserId=Guid.NewGuid() },
            binding with { FiscalMemoryId=Guid.NewGuid() },
            binding with { TaxpayerId=Guid.NewGuid() },
            binding with { InvoiceId=Guid.NewGuid() },
            binding with { Environment=TaxAuthorityEnvironment.Unknown },
            binding with { Environment=TaxAuthorityEnvironment.Sandbox } })
            Assert.Throws<NotSupportedException>(() => Phase10LegacyFrozenInvoiceProjector.Project(
                invoice,Phase10FiscalInvoiceIdentifier.Generate("ABC123",42,invoice.IssueDateUtc),
                "12345678901","98765432101",new Dictionary<Guid,string>{{Unit,"1"}},
                validatedAuthorityBinding:invalid));
    }

    [Theory]
    [InlineData(InvoiceSubject.Correction)]
    [InlineData(InvoiceSubject.Cancellation)]
    [InlineData(InvoiceSubject.SalesReturn)]
    public void Production_legal_successors_wait_for_verified_original_workflow(InvoiceSubject subject)
    {
        var invoice=Invoice();invoice.Subject=subject;
        Assert.Throws<NotSupportedException>(() => Phase10LegacyFrozenInvoiceProjector.Project(
            invoice,Phase10FiscalInvoiceIdentifier.Generate("ABC123",42,invoice.IssueDateUtc),
            "12345678901","98765432101",new Dictionary<Guid,string>{{Unit,"1"}},
            validatedAuthorityBinding:Binding(invoice)));
    }

    [Theory]
    [InlineData(0,null,1)]
    [InlineData(15,null,16)]
    [InlineData(15,"30",31)]
    [InlineData(45,"30",46)]
    public void Serial_increases_above_local_and_operator_watermarks(long local,string? configured,long expected)
        => Assert.Equal(expected,ProductionInvoiceFiscalSerialRepository.NextSerial(local,configured,[],"ABC123"));

    [Fact]
    public void Same_memory_external_serial_is_upper_bound_and_never_reused()
    {
        // Only serial digits 12-21 are serial; unrelated memory and malformed IDs do not affect allocation.
        var external=new[]{"ABC1230500000000000635","XYZ9990500000000000FF1","not-a-tax-id"};
        Assert.Equal(100,ProductionInvoiceFiscalSerialRepository.NextSerial(2,"10",external,"ABC123"));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1099511627776")]
    [InlineData("invalid")]
    public void Invalid_or_exhausted_watermark_fails_closed(string configured)
        => Assert.Throws<ArgumentException>(()=>ProductionInvoiceFiscalSerialRepository.NextSerial(0,configured,[],"ABC123"));

    [Fact]
    public void Exhausted_local_serial_fails_closed()
        => Assert.Throws<ArgumentException>(()=>ProductionInvoiceFiscalSerialRepository.NextSerial(0xffffffffffL,null,[],"ABC123"));

    private static AuthorityOperationBinding Binding(StoredInvoice invoice) => new(
        invoice.CustomerUserId,invoice.TaxpayerId,invoice.Id,Memory,TaxAuthorityEnvironment.Production);

    private static StoredInvoice Invoice()
    {
        var invoice=new StoredInvoice { Id=Guid.NewGuid(),CustomerUserId=Guid.NewGuid(),TaxpayerId=Guid.NewGuid(),
            Revision=1,State=InvoiceLifecycleState.Finalized,Subject=InvoiceSubject.Original,
            IssueDateUtc=new DateTime(2026,11,1,12,0,0,DateTimeKind.Utc) };
        invoice.SnapshotJson=JsonSerializer.Serialize(new {
            sourceWorkspaceId=invoice.Id,sourceRevision=1,environment="Production",fiscalMemoryId=Memory,
            invoiceType=1,pattern=1,buyerPersonType=2,settlementMethod=1,cashAmountRial=110,creditAmountRial=0,
            lines=new[]{new InvoiceEntryLine(Guid.NewGuid(),"service",1,100,0,10m,Unit,OfficialIdentifierSnapshot:"1234567890123")}
        });
        return invoice;
    }
}
