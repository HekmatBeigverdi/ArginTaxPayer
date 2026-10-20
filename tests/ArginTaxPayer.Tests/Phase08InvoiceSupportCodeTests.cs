using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase08InvoiceSupportCodeTests
{
    [Fact]
    public void Encoded_system_code_is_short_reversible_and_unique_for_distinct_ids()
    {
        var ids = Enumerable.Range(0, 2000).Select(_ => Guid.NewGuid()).ToArray();
        var codes = ids.Select(InvoiceSupportCode.Encode).ToArray();
        Assert.Equal(ids.Length, codes.Distinct().Count());
        foreach (var (id, code) in ids.Zip(codes))
        {
            Assert.Equal(22, code.Length);
            Assert.True(InvoiceSupportCode.TryDecode(code, out var decoded));
            Assert.Equal(id, decoded);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("........................")]
    public void Invalid_codes_do_not_decode(string? input)
    {
        Assert.False(InvoiceSupportCode.TryDecode(input, out _));
    }
}
