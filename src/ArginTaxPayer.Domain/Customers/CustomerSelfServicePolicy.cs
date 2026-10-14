namespace ArginTaxPayer.Domain.Customers;

/// <summary>
/// Explicit Phase05 allowlist; paid-plan entitlements are not implemented until Phase12.
/// Every unspecified operation fails closed.
/// </summary>
public enum CustomerSelfServiceAction
{
    ViewOwnDashboard = 1,
    ViewOwnProfile = 2,
    ViewOwnTaxpayers = 3,
    ViewOwnMemberships = 4,
    UpdateOwnProfile = 5,
    CreateTaxpayer = 6,
    UpdateTaxpayer = 7,
    LinkTaxpayer = 8,
    UnlinkTaxpayer = 9,
    SubmitFiscalInvoice = 10
}

public static class CustomerSelfServicePolicy
{
    public const string Version = "phase05.selfservice.v1";

    public static bool IsFreeReadOnlyAction(CustomerSelfServiceAction action) => action is
        CustomerSelfServiceAction.ViewOwnDashboard or
        CustomerSelfServiceAction.ViewOwnProfile or
        CustomerSelfServiceAction.ViewOwnTaxpayers or
        CustomerSelfServiceAction.ViewOwnMemberships;

    // No paid entitlements can be assumed from profile status, admin ownership or role.
    public static bool IsAllowed(CustomerSelfServiceAction action, bool isOwnActiveCustomer) =>
        isOwnActiveCustomer && IsFreeReadOnlyAction(action);
}
