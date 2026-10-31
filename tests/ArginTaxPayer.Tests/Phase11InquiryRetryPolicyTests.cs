using ArginTaxPayer.Application.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase11InquiryRetryPolicyTests
{
    private static readonly DateTime At =
        new(2026, 10, 31, 10, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("inquiry_timeout")]
    [InlineData("inquiry_network_unavailable")]
    [InlineData("inquiry_http_429")]
    [InlineData("inquiry_http_502")]
    [InlineData("inquiry_http_503")]
    [InlineData("inquiry_http_504")]
    public void Transient_inquiry_errors_allow_only_bounded_inquiry_retry(string code)
    {
        var decision = Phase11InquiryRetryPolicy.Decide(code, 0, At);
        Assert.True(decision.MayRetryInquiry);
        Assert.Equal(At.AddMinutes(1), decision.NextInquiryUtc);
        Assert.False(Phase11InquiryRetryPolicy.MayResendInvoice);
    }

    [Theory]
    [InlineData("inquiry_http_401")]
    [InlineData("inquiry_http_403")]
    [InlineData("inquiry_http_404")]
    [InlineData("inquiry_authority_rejected")]
    [InlineData("inquiry_evidence_conflict")]
    [InlineData("inquiry_result_missing")]
    [InlineData(null)]
    public void Permanent_or_inconclusive_errors_do_not_retry_automatically(string? code)
    {
        Assert.False(Phase11InquiryRetryPolicy.Decide(code, 0, At).MayRetryInquiry);
    }

    [Fact]
    public void Retry_is_limited_and_backoff_is_capped()
    {
        Assert.Equal(At.AddMinutes(16),
            Phase11InquiryRetryPolicy.Decide("inquiry_timeout", 4, At).NextInquiryUtc);
        Assert.Equal(At.AddMinutes(30),
            Phase11InquiryRetryPolicy.Decide("inquiry_timeout", 5, At).NextInquiryUtc);
        Assert.False(Phase11InquiryRetryPolicy.Decide("inquiry_timeout", 6, At).MayRetryInquiry);
    }

    [Fact]
    public void Invalid_input_fails_closed()
    {
        Assert.False(Phase11InquiryRetryPolicy.Decide("inquiry_timeout", -1, At).MayRetryInquiry);
        Assert.False(Phase11InquiryRetryPolicy.Decide("inquiry_timeout", 0, DateTime.Now).MayRetryInquiry);
    }
}
