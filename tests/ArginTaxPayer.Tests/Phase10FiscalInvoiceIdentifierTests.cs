using System;
using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10FiscalInvoiceIdentifierTests
{
    [Theory]
    [InlineData("ABC123",1,2026,10,25)]
    [InlineData("ABC123",100000,2025,3,1)]
    [InlineData("999999",1099511627775,2024,1,1)]
    public void Generated_id_has_22_characters_and_valid_check_digit(
        string memory,long serial,int year,int month,int day)
    {
        var id=Phase10FiscalInvoiceIdentifier.Generate(
            memory,serial,new DateTime(year,month,day,10,0,0,DateTimeKind.Utc));
        Assert.Equal(22,id.Length);
        Assert.StartsWith(memory,id);
        Assert.Matches("^[A-Z0-9]{22}$",id);
        var again=Phase10FiscalInvoiceIdentifier.Generate(
            memory,serial,new DateTime(year,month,day,20,0,0,DateTimeKind.Utc));
        Assert.Equal(id,again);
    }

    [Fact]
    public void Unique_serial_produces_different_fiscal_identifiers()
    {
        var now=new DateTime(2026,10,25,0,0,0,DateTimeKind.Utc);
        Assert.NotEqual(Phase10FiscalInvoiceIdentifier.Generate("ABC123",1,now),
            Phase10FiscalInvoiceIdentifier.Generate("ABC123",2,now));
    }

    [Theory]
    [InlineData("ABC1234",1)]
    [InlineData("abc123",1)]
    [InlineData("ABC123",0)]
    [InlineData("ABC123",-1)]
    [InlineData("ABC123",1099511627776)]
    public void Invalid_memory_and_serial_never_yield_tax_ids(string memory,long serial)
    {
        Assert.Throws<ArgumentException>(()=>Phase10FiscalInvoiceIdentifier.Generate(
            memory,serial,new DateTime(2026,10,25,0,0,0,DateTimeKind.Utc)));
    }
}
