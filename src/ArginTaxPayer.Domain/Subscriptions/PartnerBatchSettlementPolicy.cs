namespace ArginTaxPayer.Domain.Subscriptions;

public sealed record PartnerBatchSettlementAmounts(long GrossIrr,long ApprovedCostIrr,long RootPaidCostIrr,long PartnerShareIrr,long RootShareIrr,long NetToRootIrr);
public static class PartnerBatchSettlementPolicy
{
 public static PartnerBatchSettlementAmounts Calculate(long grossIrr,long approvedCostIrr,long rootPaidCostIrr,int partnerBasisPoints)
 {
  if(grossIrr<0)throw new ArgumentOutOfRangeException(nameof(grossIrr));
  if(approvedCostIrr<0||approvedCostIrr>grossIrr)throw new ArgumentOutOfRangeException(nameof(approvedCostIrr));
  if(rootPaidCostIrr<0||rootPaidCostIrr>approvedCostIrr)throw new ArgumentOutOfRangeException(nameof(rootPaidCostIrr));
  if(partnerBasisPoints is <0 or >10000)throw new ArgumentOutOfRangeException(nameof(partnerBasisPoints));
  var partner=(long)decimal.Truncate((decimal)(grossIrr-approvedCostIrr)*partnerBasisPoints/10000m);
  var root=grossIrr-approvedCostIrr-partner;
  return new(grossIrr,approvedCostIrr,rootPaidCostIrr,partner,root,root+rootPaidCostIrr);
 }
}
