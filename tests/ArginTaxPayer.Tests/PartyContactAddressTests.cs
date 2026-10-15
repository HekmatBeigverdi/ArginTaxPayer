using ArginTaxPayer.Domain.Parties;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Parties;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class PartyContactAddressTests
{
    [Theory]
    [InlineData(1, "۰۹۱۲۳۴۵۶۷۸۹", "09123456789")]
    [InlineData(1, "+۹۸۹۱۲۳۴۵۶۷۸۹", "+989123456789")]
    [InlineData(2, "02112345678", "02112345678")]
    [InlineData(3, " USER@EXAMPLE.COM ", "user@example.com")]
    public void Contact_golden_validation(int kind, string raw, string expected)
    {
        Assert.True(PartyContactAddressValidation.TryContact(kind, raw, null, out var value, out _));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData(1, "0912-3456789")]
    [InlineData(1, "0912\n3456789")]
    [InlineData(1, "000000000")]
    [InlineData(3, "not-email")]
    [InlineData(3, "alice@example.com\n")]
    [InlineData(2, "123")]
    [InlineData(9, "12345678")]
    public void Contact_rejects_invalid_values(int kind, string raw)
        => Assert.False(PartyContactAddressValidation.TryContact(kind, raw, null, out _, out _));

    [Fact]
    public void Address_normalizes_iran_postal_code_and_rejects_control_characters()
    {
        Assert.True(PartyContactAddressValidation.TryAddress(
            " دفتر ", "ir", "تهران", "تهران", " خیابان نمونه ۱۲ ", "۱۲۳۴۵۶۷۸۹۰", out var address));
        Assert.Equal("IR", address!.CountryCode);
        Assert.Equal("1234567890", address.PostalCode);
        Assert.False(PartyContactAddressValidation.TryAddress(
            "دفتر\n", "IR", "تهران", "تهران", "خیابان نمونه", "1234567890", out _));
        Assert.False(PartyContactAddressValidation.TryAddress(
            "دفتر", "IR", "تهران", "تهران", "خیابان نمونه", "123", out _));
    }

    [Fact]
    public void Both_entities_are_restricted_and_optimistic_concurrency_is_enabled()
    {
        using var db = new ArginTaxPayerDbContext(new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=localhost;Database=not_connected;Username=none;Password=none").Options);
        foreach (var type in new[] { typeof(PartyContact), typeof(PartyAddress) })
        {
            var entity = db.Model.FindEntityType(type)!;
            Assert.Single(entity.GetForeignKeys());
            Assert.Equal(DeleteBehavior.Restrict, entity.GetForeignKeys().Single().DeleteBehavior);
            Assert.True(entity.FindProperty("Revision")!.IsConcurrencyToken);
            Assert.Contains(entity.GetIndexes(), i => i.IsUnique &&
                i.GetFilter() == "is_active AND is_primary");
        }
    }
}
