using System.Text.Json;

namespace ArginTaxPayer.Application.Invoices;

public sealed record LegacyAuthorityIssue(string Code, string Message);

/// <summary>A display report from a trusted legacy inquiry, not legal acceptance or resend authority.</summary>
public sealed record LegacyAuthorityInquiryReport(
    string Status, IReadOnlyList<LegacyAuthorityIssue> Errors,
    IReadOnlyList<LegacyAuthorityIssue> Warnings, bool CanPrepareCorrection,
    DateTime ObservedAtUtc);

public static class LegacyAuthorityInquiryReportParser
{
    public static LegacyAuthorityInquiryReport? Parse(
        InvoiceReconciliationIdentity expected, string providerResultsJson, DateTime observedAtUtc)
    {
        // Correlation also bounds the input to 262144 characters and depth 16.
        var decision = Phase10InquiryReconciliation.Evaluate(expected, providerResultsJson);
        if (decision.Outcome != InvoiceReconciliationOutcome.CorrelatedPendingReview ||
            expected.AuthorityReference is null ||
            !string.Equals(decision.MatchedReference, expected.AuthorityReference, StringComparison.Ordinal))
            return null;

        try
        {
            using var document = JsonDocument.Parse(providerResultsJson,
                new JsonDocumentOptions { MaxDepth = 16, AllowTrailingCommas = false });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() != 1 ||
                HasDuplicateProperties(root))
                return null;

            var item = root[0];
            var status = item.GetProperty("status").GetString() switch
            {
                "FAILED" => "FAILED",
                "SUCCESS" => "SUCCESS",
                "PENDING" => "PENDING",
                _ => "UNKNOWN"
            };
            IReadOnlyList<LegacyAuthorityIssue> errors = Array.Empty<LegacyAuthorityIssue>();
            IReadOnlyList<LegacyAuthorityIssue> warnings = Array.Empty<LegacyAuthorityIssue>();
            var explicitlyFailed = false;
            if (item.TryGetProperty("data", out var data) && data.ValueKind != JsonValueKind.Null)
            {
                if (data.ValueKind != JsonValueKind.Object ||
                    !TryReadIssues(data, "error", out errors) ||
                    !TryReadIssues(data, "warning", out warnings))
                    return null;
                explicitlyFailed = data.TryGetProperty("success", out var success) &&
                    success.ValueKind == JsonValueKind.False;
            }

            return new(status, errors, warnings,
                status == "FAILED" && explicitlyFailed && errors.Count > 0, observedAtUtc);
        }
        catch (JsonException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    private static bool HasDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
                if (!names.Add(property.Name) || HasDuplicateProperties(property.Value))
                    return true;
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                if (HasDuplicateProperties(item)) return true;
        }
        return false;
    }

    private static bool TryReadIssues(JsonElement data, string property,
        out IReadOnlyList<LegacyAuthorityIssue> issues)
    {
        issues = Array.Empty<LegacyAuthorityIssue>();
        if (!data.TryGetProperty(property, out var array)) return true;
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > 100)
            return false;

        var parsed = new List<LegacyAuthorityIssue>(array.GetArrayLength());
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("code", out var codeElement) ||
                codeElement.ValueKind != JsonValueKind.String ||
                !item.TryGetProperty("message", out var messageElement) ||
                messageElement.ValueKind != JsonValueKind.String)
                return false;

            var code = codeElement.GetString()!;
            var message = messageElement.GetString()!;
            if (code.Length is 0 or > 80 || message.Length > 1000 ||
                !code.All(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or
                    >= '0' and <= '9' or '_' or '.' or '-'))
                return false;

            // Keep markup literal: consumers must render this as text, never HTML.
            var displayMessage = new string(message.Where(c => !char.IsControl(c)).ToArray());
            parsed.Add(new(code, displayMessage));
        }
        issues = parsed.AsReadOnly();
        return true;
    }
}
