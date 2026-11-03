namespace ArginTaxPayer.Domain.Subscriptions;

/// <summary>
/// A commercial administrator may approve an order on credit. Activation is
/// an entitlement decision, not evidence that cash was received.
/// Collections remain in the verified receipt ledger and must never be inferred
/// from activation.
/// </summary>
public static class CommercialActivationApprovalPolicy
{
    public static void RequireApprovable(long grossIrr, long collectedIrr,
        CommercialOrderStatus status)
    {
        if (grossIrr <= 0 || collectedIrr < 0 || collectedIrr > grossIrr ||
            status == CommercialOrderStatus.Voided)
            throw new InvalidOperationException("Only a valid, non-voided order with a consistent balance can be approved.");

        // Intentionally no 'fully paid' predicate: the approving administrator
        // accepts responsibility for an outstanding receivable.
    }

    public static long OutstandingIrr(long grossIrr, long collectedIrr)
    {
        if (grossIrr < 0 || collectedIrr < 0 || collectedIrr > grossIrr)
            throw new InvalidOperationException("Invalid receivable balance.");
        return checked(grossIrr - collectedIrr);
    }
}
