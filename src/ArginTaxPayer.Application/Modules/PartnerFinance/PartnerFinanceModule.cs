using ArginTaxPayer.Application.Modules;

namespace ArginTaxPayer.Application.Modules.PartnerFinance;

/// <summary>Architectural boundary declaration; contains no implemented business behavior.</summary>
public sealed class PartnerFinanceModule : IModuleDescriptor
{
    public string Name => "PartnerFinance";
    public string Ownership => "Expense approvals, profit distribution and settlement ledger";
    public string SecurityInvariant => "Only Root approves submitted expenses";
}
