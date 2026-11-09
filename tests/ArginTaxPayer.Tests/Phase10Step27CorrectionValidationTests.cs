using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;
public sealed class Phase10Step27CorrectionValidationTests
{
 private static InvoiceEntryLine Line(decimal q=2m,decimal price=100m,decimal vat=10m)=>
  new(Guid.Parse("11111111-1111-1111-1111-111111111111"),"خدمات",q,price,0m,vat,
   Guid.Parse("22222222-2222-2222-2222-222222222222"),"LOCAL","1234567890123");

 [Fact] public void Price_change_allowed()=>Assert.True(
  Phase10CorrectionValidation.AllowedLines([Line()],[Line(price:200)],false));
 [Fact] public void Quantity_reduction_requires_original_mistake_evidence()
 {
  Assert.False(Phase10CorrectionValidation.AllowedLines([Line()],[Line(q:1)],false));
  Assert.True(Phase10CorrectionValidation.AllowedLines([Line()],[Line(q:1)],true));
 }
 [Fact] public void Vat_change_is_forbidden()=>Assert.False(
  Phase10CorrectionValidation.AllowedLines([Line()],[Line(vat:9)],false));
 [Fact] public void Missing_line_is_forbidden()=>Assert.False(
  Phase10CorrectionValidation.AllowedLines([Line()],[],false));

 [Fact] public void Reducing_remaining_two_to_one_is_a_quantity_correction_without_a_separate_toggle()
 {
  var original = new[] { Line(q:2) };
  var edited = new[] { Line(q:1) };
  var quantityCorrection = Phase10CorrectionValidation.IsQuantityCorrection(original,edited,false);
  Assert.True(quantityCorrection);
  Assert.True(Phase10CorrectionValidation.AllowedLines(original,edited,quantityCorrection));
 }

 [Fact] public void Zero_quantity_remains_invalid_even_in_a_quantity_correction()
 {
  var original = new[] { Line(q:2) };
  var edited = new[] { Line(q:0) };
  Assert.False(Phase10CorrectionValidation.AllowedLines(original,edited,
   Phase10CorrectionValidation.IsQuantityCorrection(original,edited,false)));
 }

 [Fact] public void Price_edit_does_not_record_a_quantity_correction() => Assert.False(
  Phase10CorrectionValidation.IsQuantityCorrection([Line(q:2)],[Line(q:2,price:200)],false));
}
