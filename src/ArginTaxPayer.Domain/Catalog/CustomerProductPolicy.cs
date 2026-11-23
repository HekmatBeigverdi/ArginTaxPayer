using System.Text;

namespace ArginTaxPayer.Domain.Catalog;

/// <summary>
/// Tenant-owned ArginAccounting Item kinds. Official fiscal goods/service IDs are separate.
/// </summary>
public enum CatalogItemKind { StockItem = 1, NonStockItem = 2, Service = 3 }
public enum CatalogItemStatus { Active = 1, Inactive = 2, Blocked = 3, Discontinued = 4 }

public sealed record CatalogProductInput(
    string InternalCode, string Title, CatalogItemKind Kind, Guid BaseUnitId,
    string? OfficialCode = null, decimal? VatRatePercent = null);

public static class CustomerProductPolicy
{
    public static bool TryValidate(CatalogProductInput? input,
        out CatalogProductInput? normalized)
    {
        normalized = null;
        if (input is null || input.BaseUnitId == Guid.Empty ||
            input.Kind is not (CatalogItemKind.StockItem or CatalogItemKind.NonStockItem) ||
            input.VatRatePercent is { } rate &&
                (rate is < 0m or > 999.99m || rate != decimal.Round(rate, 2)))
            return false;
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
        // Canonical customer code, not an authority identifier.
        normalized = new CatalogProductInput(code.ToUpperInvariant(), title, input.Kind, input.BaseUnitId,
            string.IsNullOrEmpty(officialCode) ? null : officialCode,input.VatRatePercent);
        return true;
    }

    public static bool CanEdit(CatalogItemStatus status) =>
        status is CatalogItemStatus.Active or CatalogItemStatus.Inactive;

    public static bool CanChangeKind(CatalogItemKind oldKind, CatalogItemKind newKind) =>
        oldKind is CatalogItemKind.StockItem or CatalogItemKind.NonStockItem &&
        newKind is CatalogItemKind.StockItem or CatalogItemKind.NonStockItem;

    public static bool IsArchived(CatalogItemStatus status) =>
        status == CatalogItemStatus.Discontinued;
}
