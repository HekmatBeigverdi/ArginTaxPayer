using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10DispatchEvidenceTests
{
    private static readonly DateTime Observed = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Evidence_hash_is_stable_and_contains_no_provider_body()
    {
        var first = Phase10DispatchEvidenceRepository.EvidenceHash(
            "invoice.dispatch.initial_receipt_recorded", "REF-001", Observed);
        var second = Phase10DispatchEvidenceRepository.EvidenceHash(
            "invoice.dispatch.initial_receipt_recorded", "REF-001", Observed);

        Assert.Equal(first, second);
        Assert.Matches("^[a-f0-9]{64}$", first);
        Assert.DoesNotContain("REF-001", first);
    }

    [Theory]
    [InlineData("raw provider response", "REF-001")]
    [InlineData("invoice.dispatch.response_indeterminate", "bad reference\n")]
    public void Evidence_hash_rejects_unsanitized_values(string code, string reference)
    {
        Assert.Throws<ArgumentException>(() =>
            Phase10DispatchEvidenceRepository.EvidenceHash(code, reference, Observed));
    }
}
