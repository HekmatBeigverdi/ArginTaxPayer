using System;
using System.Text.Json;
using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10Step26CancellationWireFactoryTests
{
    private static readonly InvoiceTypePattern Pattern =
        new(ElectronicInvoiceType.First, 1);

    [Fact]
    public void Cancellation_has_new_taxid_and_predecessor_taxid_with_no_lines()
    {
        var result=Phase10CancellationWireFactory.Project(
            "AAAAAAAAAAAAAAAAAAAAAA","BBBBBBBBBBBBBBBBBBBBBB",
            "12345678901",1790000000000,Pattern);
        using var json=JsonDocument.Parse(result.Json);
        var header=json.RootElement.GetProperty("header");
        Assert.Equal(3,header.GetProperty("ins").GetInt32());
        Assert.Equal("AAAAAAAAAAAAAAAAAAAAAA",header.GetProperty("taxid").GetString());
        Assert.Equal("BBBBBBBBBBBBBBBBBBBBBB",header.GetProperty("irtaxid").GetString());
        Assert.Equal(0,json.RootElement.GetProperty("body").GetArrayLength());
    }

    [Fact]
    public void Cancellation_cannot_reuse_predecessor_taxid()
    {
        Assert.Throws<ArgumentException>(()=>Phase10CancellationWireFactory.Project(
            "AAAAAAAAAAAAAAAAAAAAAA","AAAAAAAAAAAAAAAAAAAAAA",
            "12345678901",1790000000000,Pattern));
    }

    [Fact]
    public void Cancellation_rejects_malformed_reference()
    {
        Assert.Throws<ArgumentException>(()=>Phase10CancellationWireFactory.Project(
            "AAAAAAAAAAAAAAAAAAAAAA","invalid",
            "12345678901",1790000000000,Pattern));
    }
}
