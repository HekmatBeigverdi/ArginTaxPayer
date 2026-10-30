using ArginTaxPayer.Application.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10AuthorityBatchResponseParserTests
{
    private const string Uid = "1edb605b-7976-445b-9338-27598f0f92ab";
    private const string PacketType = "INVOICE.V01";
    private const string Valid = """
        {
          "timestamp": 1792764000000,
          "result": [{
            "uid": "1edb605b-7976-445b-9338-27598f0f92ab",
            "packetType": "INVOICE.V01",
            "referenceNumber": "synthetic-ref-01",
            "data": ""
          }]
        }
        """;

    [Fact]
    public void Correlated_reference_is_only_initial_receipt_not_fiscal_acceptance()
    {
        var r = Phase10AuthorityBatchResponseParser.Parse(Valid, Uid, PacketType);
        Assert.Equal(InvoiceAuthorityResponseClassification.InitialReceipt, r.Classification);
        Assert.Equal("synthetic-ref-01", r.ReferenceNumber);
        Assert.False(r.IsFiscalAccepted);
        Assert.False(r.MayAutomaticallyRetry);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{")]
    [InlineData("{\"result\":[]}")]
    [InlineData("{\"result\":null}")]
    [InlineData("{\"result\":[{}]}")]
    [InlineData("{\"result\":[{\"uid\":\"different\",\"packetType\":\"INVOICE.V01\",\"referenceNumber\":\"ref\"}]}")]
    [InlineData("{\"result\":[{\"uid\":\"1edb605b-7976-445b-9338-27598f0f92ab\",\"packetType\":\"OTHER\",\"referenceNumber\":\"ref\"}]}")]
    [InlineData("{\"result\":[{\"uid\":\"1edb605b-7976-445b-9338-27598f0f92ab\",\"packetType\":\"INVOICE.V01\",\"referenceNumber\":\"\",\"data\":\"SUCCESS\"}]}")]
    public void Invalid_or_unmatched_payload_is_indeterminate(string json)
    {
        var r = Phase10AuthorityBatchResponseParser.Parse(json, Uid, PacketType);
        Assert.Equal(InvoiceAuthorityResponseClassification.Indeterminate, r.Classification);
        Assert.Null(r.ReferenceNumber);
        Assert.False(r.IsFiscalAccepted);
    }

    [Fact]
    public void Duplicate_properties_and_provider_errors_are_not_silently_accepted()
    {
        var duplicate = Valid.Replace("\"timestamp\": 1792764000000,",
            "\"timestamp\": 1792764000000, \"RESULT\": [],");
        var error = Valid.Replace("\"timestamp\": 1792764000000,",
            "\"timestamp\": 1792764000000, \"errors\": [{\"message\":\"private response\"}],");
        foreach (var json in new[] { duplicate, error })
        {
            var r = Phase10AuthorityBatchResponseParser.Parse(json, Uid, PacketType);
            Assert.Equal(InvoiceAuthorityResponseClassification.Indeterminate, r.Classification);
            Assert.DoesNotContain("private", r.SafeCode);
        }
    }

    [Fact]
    public void Multi_packet_batch_cannot_be_bound_to_single_operation()
    {
        var multi = Valid.Replace("}]", "}, {\"uid\":\"other\"}]");
        Assert.Equal(InvoiceAuthorityResponseClassification.Indeterminate,
            Phase10AuthorityBatchResponseParser.Parse(multi, Uid, PacketType).Classification);
    }

    [Fact]
    public void Provider_success_word_cannot_create_fiscal_acceptance()
    {
        var annotated = Valid.Replace("\"data\": \"\"",
            "\"data\": \"SUCCESS\", \"status\": \"SUCCESS\"");
        var r = Phase10AuthorityBatchResponseParser.Parse(annotated, Uid, PacketType);
        Assert.Equal(InvoiceAuthorityResponseClassification.InitialReceipt, r.Classification);
        Assert.False(r.IsFiscalAccepted);
    }

    [Fact]
    public void Invalid_uid_or_type_is_rejected()
    {
        Assert.Equal(InvoiceAuthorityResponseClassification.Indeterminate,
            Phase10AuthorityBatchResponseParser.Parse(Valid, "unexpected value", PacketType).Classification);
        Assert.Equal(InvoiceAuthorityResponseClassification.Indeterminate,
            Phase10AuthorityBatchResponseParser.Parse(Valid, Uid, "").Classification);
    }
}
