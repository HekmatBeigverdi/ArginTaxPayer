using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase08HumanInvoiceReferenceTests
{
    [Fact]
    public void Reference_has_unique_source_and_system_code()
    {
        var opts = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=argintaxpayer_test;Username=none;Password=none")
            .Options;
        using var db = new ArginTaxPayerDbContext(opts);
        var entity = db.Model.FindEntityType(typeof(InvoiceHumanReference))!;
        Assert.Equal("invoice_support_references", entity.GetTableName());
        Assert.Contains(entity.GetIndexes(), x => x.IsUnique &&
            x.Properties.Select(p => p.Name).SequenceEqual(
                new[] { "SourceKind", "SourceId" }));
        Assert.Contains(entity.GetIndexes(), x => x.IsUnique &&
            x.Properties.Single().Name == "SystemCode");
    }

    [Theory]
    [InlineData("00001-05-AAA", true)]
    [InlineData("99999-05-MTU", true)]
    [InlineData("12345-05-MTU", true)]
    [InlineData("100000-05-MTU", true)]
    [InlineData("1000000-06-MTU", true)]
    [InlineData("00001-05-mtu", false)]
    public void Public_format_follows_five_digit_year_admin_pattern(string code, bool expected)
    {
        Assert.Equal(expected, System.Text.RegularExpressions.Regex.IsMatch(
            code, @"^[0-9]{5,}-[0-9]{2}-[A-Z]{3}$"));
    }
}
