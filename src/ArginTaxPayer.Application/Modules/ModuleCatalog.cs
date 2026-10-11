namespace ArginTaxPayer.Application.Modules;

/// <summary>Explicit inventory matching the Phase 01 bounded-context architecture.</summary>
public static class ModuleCatalog
{
    public static IReadOnlyList<IModuleDescriptor> All { get; } =
        Array.AsReadOnly<IModuleDescriptor>(new IModuleDescriptor[]
        {
            new IdentityAccess.IdentityAccessModule(),
            new Delegation.DelegationModule(),
            new TaxpayerMemory.TaxpayerMemoryModule(),
            new PartyCatalog.PartyCatalogModule(),
            new FiscalRules.FiscalRulesModule(),
            new Invoicing.InvoicingModule(),
            new FiscalGateway.FiscalGatewayModule(),
            new Subscription.SubscriptionModule(),
            new Collections.CollectionsModule(),
            new PartnerFinance.PartnerFinanceModule(),
            new ImportIntegration.ImportIntegrationModule(),
            new Reporting.ReportingModule(),
            new Administration.AdministrationModule(),
        });
}
