using System.Security.Cryptography;
using System.Text;
using ArginTaxPayer.Domain.Catalog;

namespace ArginTaxPayer.Infrastructure.Persistence.Catalog;

public enum ReferenceImportKind { Units = 1, GoodsServices = 2 }
public sealed record ReferenceImportIssue(int Line, string Code, string Message);
public sealed record ReferenceImportRow(string Code, string Title, int Kind);
public sealed record ReferenceImportPreview(
    ReferenceImportKind Kind, string SourceDocument, string Edition, string Sha256,
    string SourceReference, IReadOnlyList<ReferenceImportRow> Rows,
    IReadOnlyList<ReferenceImportIssue> Issues)
{
    public bool IsValid => Issues.Count == 0 && Rows.Count > 0;
}

/// <summary>Bounded preview parser; accepts canonical UTF-8 PSV only, not arbitrary Excel/PDF.</summary>
public static class ReferenceImportParser
{
    public const int MaxBytes = 2_000_000;
    public const int MaxRows = 20000;
    public const int MaxIssues = 100;
    public static ReferenceImportPreview Preview(
        ReferenceImportKind kind, string? sourceDocument, string? edition,
        string? sourceReference, byte[]? payload)
    {
        var issues = new List<ReferenceImportIssue>();
        var rows = new List<ReferenceImportRow>();
        var doc = sourceDocument?.Trim() ?? "";
        var ver = edition?.Trim() ?? "";
        var reference = sourceReference?.Trim() ?? "";
        if (kind is not (ReferenceImportKind.Units or ReferenceImportKind.GoodsServices) ||
            doc != (kind == ReferenceImportKind.Units ? "RC_UMGS.ST" : "RC_GS") ||
            ver.Length is < 1 or > 80 || reference.Length is < 8 or > 1000 ||
            !Uri.TryCreate(reference, UriKind.Absolute, out var url) ||
            (url.Scheme != Uri.UriSchemeHttps))
            issues.Add(new(0, "", "invalid_document_edition_or_https_source"));
        if (payload is null || payload.Length is < 1 or > MaxBytes)
            issues.Add(new(0, "", "invalid_file_size"));
        if (issues.Count > 0)
            return new(kind, doc, ver, "", reference, rows, issues);
        string content;
        try { content = new UTF8Encoding(false, true).GetString(payload!); }
        catch (DecoderFallbackException)
        {
            issues.Add(new(0, "", "invalid_utf8"));
            return new(kind, doc, ver, "", reference, rows, issues);
        }
        if (content.StartsWith("\ufeff", StringComparison.Ordinal))
            content = content[1..];
        var sha = Convert.ToHexString(SHA256.HashData(payload!)).ToLowerInvariant();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var lines = content.Split('\n');
        if (lines.Length > MaxRows + 2)
            issues.Add(new(0, "", "too_many_rows"));
        for (int i = 0; i < lines.Length && i < MaxRows && issues.Count < MaxIssues; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (line.Length == 0) continue;
            var fields = line.Split('|');
            // Unit: official-code|title  Goods/Service: official-code|title|Goods|Service
            var expectedFields = kind == ReferenceImportKind.Units ? 2 : 3;
            if (fields.Length != expectedFields || fields.Any(x => x.Length == 0))
            {
                issues.Add(new(i + 1, "", "invalid_columns"));
                continue;
            }
            var codeInput = fields[0].Trim();
            var valid = kind == ReferenceImportKind.Units
                ? OfficialUnitCodePolicy.TryNormalize(codeInput, out _)
                : OfficialGoodsServiceIdentifierPolicy.TryNormalize(codeInput, out _);
            var code = codeInput;
            if (valid)
                valid = kind == ReferenceImportKind.Units
                    ? OfficialUnitCodePolicy.TryNormalize(codeInput, out code)
                    : OfficialGoodsServiceIdentifierPolicy.TryNormalize(codeInput, out code);
            if (!valid || !OfficialGoodsServiceIdentifierPolicy.TryValidateTitle(fields[1], out var title))
            {
                issues.Add(new(i + 1, codeInput, "invalid_code_or_title"));
                continue;
            }
            int itemKind = 0;
            if (kind == ReferenceImportKind.GoodsServices &&
                (fields[2] is not ("Goods" or "Service")))
            {
                issues.Add(new(i + 1, code, "invalid_goods_service_kind"));
                continue;
            }
            if (kind == ReferenceImportKind.GoodsServices)
                itemKind = fields[2] == "Goods" ? 1 : 2;
            if (!seen.Add(kind == ReferenceImportKind.Units ? code : itemKind + ":" + code))
            {
                issues.Add(new(i + 1, code, "duplicate_code"));
                continue;
            }
            rows.Add(new(code, title, itemKind));
        }
        if (rows.Count == 0) issues.Add(new(0, "", "empty_import"));
        return new(kind, doc, ver, sha, reference, rows, issues);
    }
}
