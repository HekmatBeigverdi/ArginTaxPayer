namespace ArginTaxPayer.Domain.Taxpayers;

/// <summary>Temporary Phase09 policy until the plan/entitlement phase supplies a limit.</summary>
public static class TaxpayerCapacityPolicy
{
    public static bool IsAllowed(bool planCapacityConfigured, int activeTaxpayerCount, int? maximumTaxpayers)
    {
        if (activeTaxpayerCount < 0 || maximumTaxpayers is <= 0)
            return false;
        if (!planCapacityConfigured)
            return true;
        return maximumTaxpayers is not null && activeTaxpayerCount < maximumTaxpayers.Value;
    }
}
