using ArginTaxPayer.Domain.Invoices;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>Archived V7.9 correction restrictions checked against immutable predecessor lines.</summary>
public static class Phase10CorrectionValidation
{
 public static bool AllowedLines(
  IReadOnlyList<InvoiceEntryLine> original,
  IReadOnlyList<InvoiceEntryLine> changed,
  bool correctingOriginalQuantityMistake)
 {
  if(original.Count==0||original.Count!=changed.Count)return false;
  for(var i=0;i<original.Count;i++)
  {
   var old=original[i];var next=changed[i];
   if(old.CatalogItemId!=next.CatalogItemId||
      old.UnitId!=next.UnitId||
      old.OfficialIdentifierSnapshot!=next.OfficialIdentifierSnapshot||
      old.CatalogCodeSnapshot!=next.CatalogCodeSnapshot||
      old.VatRatePercent!=next.VatRatePercent||
      old.Description!=next.Description||
      next.Quantity<=0||next.UnitPriceRial<=0||next.DiscountPerUnitRial<0||
      next.DiscountPerUnitRial>next.UnitPriceRial||
      (next.Quantity<old.Quantity&&!correctingOriginalQuantityMistake))
    return false;
  }
  return true;
 }
}
