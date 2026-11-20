using System.Text;

namespace ArginTaxPayer.Domain.Catalog;

/// <summary>
/// Services are tenant-owned intangible items; they never carry stock movements.
/// InternalCode is a customer-scoped identifier, NOT an official fiscal sstid.
/// </summary>
public sealed record CatalogServiceInput(
    string InternalCode, string Title, Guid BaseUnitId, string? OfficialCode = null);

public static class CustomerServicePolicy
{
    public static bool TryValidate(CatalogServiceInput? input,
        out CatalogServiceInput? normalized)
    {
        normalized = null;
        if (input is null || input.BaseUnitId == Guid.Empty) return false;
        var code = input.InternalCode?.Trim().Normalize(NormalizationForm.FormC);
        var title = input.Title?.Trim().Normalize(NormalizationForm.FormC);
        var officialCode = input.OfficialCode?.Trim();
        if (string.IsNullOrEmpty(code) || code.Length > 100 ||
            string.IsNullOrEmpty(title) || title.Length is < 2 or > 256 ||
            code.Any(char.IsControl) || title.Any(char.IsControl) ||
            code.Contains('|') || code.Contains('\n'))
            return false;
        if (!string.IsNullOrEmpty(officialCode) &&
            (officialCode.Length != 13 || !officialCode.All(char.IsAsciiDigit))) return false;

        normalized = new CatalogServiceInput(code.ToUpperInvariant(), title, input.BaseUnitId,
            string.IsNullOrEmpty(officialCode) ? null : officialCode);
        return true;
    }

    public static bool IsService(CatalogItemKind kind) => kind == CatalogItemKind.Service;

    public static bool CanEdit(CatalogItemStatus status) =>
        status is CatalogItemStatus.Active or CatalogItemStatus.Inactive;
}
