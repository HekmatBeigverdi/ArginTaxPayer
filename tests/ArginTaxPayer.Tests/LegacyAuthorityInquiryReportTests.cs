using System.Text.Json;
using ArginTaxPayer.Application.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class LegacyAuthorityInquiryReportTests
{
    private static readonly DateTime Observed = new(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);
    private static InvoiceReconciliationIdentity Identity() => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        "packet-123", "INVOICE.V01", "ABC123", "REF123");

    private static string Response(string status = "FAILED", string? data = null) =>
        "[{\"uid\":\"packet-123\",\"packetType\":\"INVOICE.V01\",\"fiscalId\":\"ABC123\",\"referenceNumber\":\"REF123\",\"status\":" +
        JsonSerializer.Serialize(status) + (data is null ? "" : ",\"data\":" + data) + "}]";

    private static string Issue(string code = "E_1.2-3", string message = "خطای صورتحساب") =>
        JsonSerializer.Serialize(new { code, message }, new JsonSerializerOptions
        { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    private static string FailedData(string? errors = null, string? warnings = null) =>
        "{\"success\":false,\"error\":" + (errors ?? "[" + Issue() + "]") +
        ",\"warning\":" + (warnings ?? "[]") + "}";

    private static LegacyAuthorityInquiryReport? Parse(string json) =>
        LegacyAuthorityInquiryReportParser.Parse(Identity(), json, Observed);

    [Fact]
    public void Correlated_failed_report_exposes_errors_warnings_and_observation()
    {
        var report = Parse(Response(data: FailedData(
            "[" + Issue("0101", "شناسه نامعتبر") + "," + Issue("E_2.0-1", "مبلغ نادرست") + "]",
            "[" + Issue("W1", "هشدار") + "]")));
        Assert.NotNull(report);
        Assert.Equal("FAILED", report.Status);
        Assert.True(report.CanPrepareCorrection);
        Assert.Equal(Observed, report.ObservedAtUtc);
        Assert.Equal(DateTimeKind.Utc, report.ObservedAtUtc.Kind);
        Assert.Equal(new[] { new LegacyAuthorityIssue("0101", "شناسه نامعتبر"),
            new LegacyAuthorityIssue("E_2.0-1", "مبلغ نادرست") }, report.Errors);
        Assert.Equal(new LegacyAuthorityIssue("W1", "هشدار"), Assert.Single(report.Warnings));
    }

    [Fact]
    public void Archived_legacy_invoice_confirmation_type_preserves_correlated_fiscal_errors()
    {
        var response=Response(data:FailedData()).Replace("INVOICE.V01","receive_invoice_confirm");
        var report=Parse(response);
        Assert.NotNull(report);
        Assert.Equal("FAILED",report.Status);
        Assert.True(report.CanPrepareCorrection);
        Assert.Single(report.Errors);
    }

    [Theory]
    [InlineData("packet-123", "other-uid")]
    [InlineData("REF123", "OTHERREF")]
    [InlineData("ABC123", "OTHER123")]
    [InlineData("INVOICE.V01", "OTHER")]
    public void Mismatched_identity_is_rejected(string oldValue, string newValue) =>
        Assert.Null(Parse(Response(data: FailedData()).Replace(oldValue, newValue)));

    [Fact]
    public void Expected_reference_is_required() => Assert.Null(
        LegacyAuthorityInquiryReportParser.Parse(Identity() with { AuthorityReference = null },
            Response(data: FailedData()), Observed));

    [Fact]
    public void Invalid_local_identity_cannot_bypass_reconciliation() => Assert.Null(
        LegacyAuthorityInquiryReportParser.Parse(Identity() with { OperationId = Guid.Empty },
            Response(data: FailedData()), Observed));

    [Theory]
    [InlineData("SUCCESS", "SUCCESS")]
    [InlineData("PENDING", "PENDING")]
    [InlineData("NOT_FOUND", "UNKNOWN")]
    [InlineData("UNKNOWN", "UNKNOWN")]
    [InlineData("failed", "UNKNOWN")]
    [InlineData(" FAILED", "UNKNOWN")]
    public void Only_exact_failed_status_can_allow_correction(string status, string normalized)
    {
        var report = Parse(Response(status, FailedData()));
        Assert.NotNull(report);
        Assert.Equal(normalized, report.Status);
        Assert.False(report.CanPrepareCorrection);
    }

    [Fact]
    public void Successful_provider_result_remains_a_display_report()
    {
        var report = Parse(Response("SUCCESS", "{\"success\":true,\"error\":[],\"warning\":[]}"));
        Assert.NotNull(report);
        Assert.Equal("SUCCESS", report.Status);
        Assert.False(report.CanPrepareCorrection);
        Assert.Empty(report.Errors);
        Assert.Empty(report.Warnings);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    public void Pending_allows_missing_or_null_data(string? data)
    {
        var report = Parse(Response("PENDING", data));
        Assert.NotNull(report);
        Assert.Equal("PENDING", report.Status);
        Assert.False(report.CanPrepareCorrection);
        Assert.Empty(report.Errors);
        Assert.Empty(report.Warnings);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"success\":false,\"error\":[]}")]
    [InlineData("{\"success\":true,\"error\":[{\"code\":\"E1\",\"message\":\"error\"}]}")]
    [InlineData("{\"error\":[{\"code\":\"E1\",\"message\":\"error\"}]}")]
    [InlineData("{\"success\":\"false\",\"error\":[{\"code\":\"E1\",\"message\":\"error\"}]}")]
    public void Failed_requires_boolean_false_and_an_error(string? data)
    {
        var report = Parse(Response(data: data));
        Assert.NotNull(report);
        Assert.False(report.CanPrepareCorrection);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("[{}]")]
    [InlineData("[null]")]
    [InlineData("null")]
    public void Malformed_json_or_result_is_rejected(string json) => Assert.Null(Parse(json));

    [Fact]
    public void Multiple_results_are_rejected()
    {
        var item = Response(data: FailedData())[1..^1];
        Assert.Null(Parse("[" + item + "," + item + "]"));
    }

    [Theory]
    [InlineData("{\"success\":false,\"success\":true}")]
    [InlineData("{\"success\":false,\"SUCCESS\":true}")]
    [InlineData("{\"extra\":[{\"x\":1,\"x\":2}]}")]
    [InlineData("{\"error\":[{\"code\":\"E1\",\"co\\u0064e\":\"E2\",\"message\":\"error\"}]}")]
    public void Nested_duplicate_properties_are_rejected(string data) =>
        Assert.Null(Parse(Response(data: data)));

    [Fact]
    public void Top_level_duplicates_are_rejected() => Assert.Null(Parse(
        Response(data: FailedData()).Replace("\"status\":", "\"STATUS\":\"PENDING\",\"status\":")));

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[null]")]
    [InlineData("[1]")]
    [InlineData("[{}]")]
    [InlineData("[{\"code\":1,\"message\":\"error\"}]")]
    [InlineData("[{\"code\":\"E1\",\"message\":null}]")]
    [InlineData("[{\"code\":\"\",\"message\":\"error\"}]")]
    [InlineData("[{\"code\":\"E 1\",\"message\":\"error\"}]")]
    [InlineData("[{\"code\":\"۱۲\",\"message\":\"error\"}]")]
    public void Malformed_errors_or_warnings_fail_closed(string issues)
    {
        Assert.Null(Parse(Response(data: FailedData(errors: issues))));
        Assert.Null(Parse(Response(data: FailedData(warnings: issues))));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("true")]
    [InlineData("\"data\"")]
    public void Non_object_data_is_rejected(string data) => Assert.Null(Parse(Response(data: data)));

    [Fact]
    public void Issue_limits_are_inclusive_and_overflows_fail_closed()
    {
        var issue = Issue(new string('A', 80), new string('م', 1000));
        var hundred = "[" + string.Join(",", Enumerable.Repeat(issue, 100)) + "]";
        var report = Parse(Response(data: FailedData(hundred, hundred)));
        Assert.NotNull(report);
        Assert.True(report.CanPrepareCorrection);
        Assert.Equal(100, report.Errors.Count);
        Assert.Equal(100, report.Warnings.Count);
        foreach (var invalid in new[] {
            "[" + string.Join(",", Enumerable.Repeat(Issue(), 101)) + "]",
            "[" + Issue(new string('A', 81)) + "]",
            "[" + Issue(message: new string('م', 1001)) + "]" })
        {
            Assert.Null(Parse(Response(data: FailedData(errors: invalid))));
            Assert.Null(Parse(Response(data: FailedData(warnings: invalid))));
        }
    }

    [Fact]
    public void Json_size_and_depth_are_bounded()
    {
        var json = Response("PENDING");
        Assert.NotNull(Parse(json.PadRight(262144)));
        Assert.Null(Parse(json.PadRight(262145)));
        Assert.NotNull(Parse(Response(data: "{\"extra\":" + new string('[', 13) + "0" + new string(']', 13) + "}")));
        Assert.Null(Parse(Response(data: "{\"extra\":" + new string('[', 14) + "0" + new string(']', 14) + "}")));
    }

    [Fact]
    public void Messages_preserve_html_as_text_and_Persian_but_remove_controls()
    {
        const string text = "<script>alert('x')</script><b>خطای می‌شود</b>&amp;";
        var report = Parse(Response(data: FailedData("[" + Issue(message: text + "\0\u001b\u007f") + "]")));
        Assert.NotNull(report);
        Assert.Equal(text, Assert.Single(report.Errors).Message);
    }
}
