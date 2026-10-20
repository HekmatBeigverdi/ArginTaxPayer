namespace ArginTaxPayer.Application.Tenancy;

/// <summary>
/// Phase08 authorization decision over server-resolved, CURRENT relationships.
/// This policy is never a source of ownership evidence: the caller must resolve
/// actor role, active customer-to-taxpayer membership and commercial ownership
/// using authenticated identity and scoped database queries.
/// </summary>
public enum InvoiceWorkflowRole { Customer = 1, Level2 = 2, Level1 = 3 }
public enum InvoiceWorkflowPermission { Read = 1, EditDraft = 2, SubmitForReview = 3, RequestChanges = 4, Approve = 5 }
public enum InvoiceWorkflowAccessDecision { Allowed = 1, Denied = 2 }

public sealed record InvoiceWorkflowScope
{
    public Guid ActorUserId { get; }
    public Guid CustomerUserId { get; }
    public Guid TaxpayerId { get; }
    public Guid ControllingLevel1UserId { get; }
    public Guid? CurrentPartnerLevel2UserId { get; }
    public bool ActiveCustomerTaxpayerMembership { get; }

    public InvoiceWorkflowScope(Guid actorUserId, Guid customerUserId, Guid taxpayerId,
        Guid controllingLevel1UserId, Guid? currentPartnerLevel2UserId,
        bool activeCustomerTaxpayerMembership)
    {
        if (actorUserId == Guid.Empty || customerUserId == Guid.Empty ||
            taxpayerId == Guid.Empty || controllingLevel1UserId == Guid.Empty ||
            currentPartnerLevel2UserId == Guid.Empty)
            throw new ArgumentException("Invalid live invoice scope identifiers.");
        ActorUserId = actorUserId;
        CustomerUserId = customerUserId;
        TaxpayerId = taxpayerId;
        ControllingLevel1UserId = controllingLevel1UserId;
        CurrentPartnerLevel2UserId = currentPartnerLevel2UserId;
        ActiveCustomerTaxpayerMembership = activeCustomerTaxpayerMembership;
    }
}

/// <summary>
/// A fail-closed, side-effect-free permission matrix. A forged scope object
/// can never authorize API use: no endpoints may trust these values from clients.
/// Caller must also verify session/MFA, invoice ownership, current root/partner
/// relationship, and revision atomically in infrastructure.
/// </summary>
public static class InvoiceWorkflowAccessPolicy
{
    public static InvoiceWorkflowAccessDecision Evaluate(
        InvoiceWorkflowRole role, InvoiceWorkflowPermission permission,
        InvoiceWorkflowScope? scope)
    {
        if (scope is null || !scope.ActiveCustomerTaxpayerMembership ||
            !Enum.IsDefined(role) || !Enum.IsDefined(permission))
            return InvoiceWorkflowAccessDecision.Denied;

        var owns = role switch
        {
            InvoiceWorkflowRole.Customer => scope.ActorUserId == scope.CustomerUserId,
            InvoiceWorkflowRole.Level1 => scope.ActorUserId == scope.ControllingLevel1UserId,
            InvoiceWorkflowRole.Level2 => scope.CurrentPartnerLevel2UserId == scope.ActorUserId,
            _ => false
        };
        if (!owns) return InvoiceWorkflowAccessDecision.Denied;
        var permitted = permission switch
        {
            InvoiceWorkflowPermission.Read => true,
            InvoiceWorkflowPermission.EditDraft or InvoiceWorkflowPermission.SubmitForReview =>
                role == InvoiceWorkflowRole.Customer,
            InvoiceWorkflowPermission.RequestChanges or InvoiceWorkflowPermission.Approve =>
                role is InvoiceWorkflowRole.Level1 or InvoiceWorkflowRole.Level2,
            _ => false
        };
        return permitted ? InvoiceWorkflowAccessDecision.Allowed : InvoiceWorkflowAccessDecision.Denied;
    }
}
