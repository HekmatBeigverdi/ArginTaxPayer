using System.Text.Json;
namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;
internal static class InvoiceSnapshotClassification
{
    public static bool IsType2GeneralSale(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object &&
        root.TryGetProperty("invoiceType", out var type) && type.ValueKind == JsonValueKind.Number && type.TryGetInt32(out var t) && t == 2 &&
        root.TryGetProperty("pattern", out var pattern) && pattern.ValueKind == JsonValueKind.Number && pattern.TryGetInt32(out var p) && p == 1;
    public static bool MissingContractNumber(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object &&
        root.TryGetProperty("invoiceType", out var type) && type.ValueKind == JsonValueKind.Number && type.TryGetInt32(out var t) && t == 1 &&
        root.TryGetProperty("pattern", out var pattern) && pattern.ValueKind == JsonValueKind.Number && pattern.TryGetInt32(out var p) && p == 4 &&
        (!root.TryGetProperty("contractNumber", out var contract) || contract.ValueKind != JsonValueKind.String ||
         contract.GetString() is not { Length: > 0 and <= 12 } code || !code.All(char.IsAsciiDigit));
}
