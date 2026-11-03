namespace ArginTaxPayer.Domain.Subscriptions;

/// <summary>Partner earnings are based on confirmed cash collection, never issued invoices.</summary>
public sealed record PartnerShareTerms(int PartnerBasisPoints)
{
    public void Validate()
    {
        if (PartnerBasisPoints is < 0 or > 10000)
            throw new ArgumentOutOfRangeException(nameof(PartnerBasisPoints));
    }
}

public sealed record VerifiedRevenueAllocation(long CollectedIrr, long ApprovedCostIrr,
    long NetDistributableIrr, long PartnerShareIrr, long RootShareIrr);

public static class PartnerRevenuePolicy
{
    public static VerifiedRevenueAllocation Calculate(long verifiedCollectedIrr,
        long approvedCostIrr, PartnerShareTerms terms)
    {
        terms.Validate();
        if (verifiedCollectedIrr < 0 || approvedCostIrr < 0 ||
            approvedCostIrr > verifiedCollectedIrr)
            throw new InvalidOperationException("Invalid verified collection or approved cost.");
        long net = checked(verifiedCollectedIrr - approvedCostIrr);
        // Use integer arithmetic to avoid fractional IRR and round toward zero.
        long partner = checked(net * terms.PartnerBasisPoints / 10000L);
        return new(verifiedCollectedIrr, approvedCostIrr, net, partner, net - partner);
    }
}
