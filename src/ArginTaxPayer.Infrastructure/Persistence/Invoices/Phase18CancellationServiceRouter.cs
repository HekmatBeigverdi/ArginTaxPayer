using ArginTaxPayer.Domain.Invoices;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public sealed class Phase18CancellationServiceRouter(
    Phase18InvoiceMemoryBindingService memories,
    Phase18ProductionCancellationService production,
    Phase10SandboxCancellationService sandbox,
    Phase10CancellationEligibilityService sandboxEligibility)
{
    public async Task<CancellationSendResult> SendAsync(string? bearer,Guid taxpayerId,Guid invoiceId,CancellationToken ct)
    {
        var binding=await memories.ResolveAsync(bearer,taxpayerId,invoiceId,ct);
        if(binding.Decision!=InvoiceApiDecision.Success)return new(binding.Decision,binding.Code);
        if(binding.Environment==TaxAuthorityEnvironment.Production)return await production.SendAsync(bearer,taxpayerId,invoiceId,ct);
        if(binding.LegacySandbox)return await sandbox.SendAsync(bearer,taxpayerId,invoiceId,ct);
        return new(InvoiceApiDecision.Conflict,"classified_sandbox_legal_successor_not_supported");
    }

    public async Task<Phase10CancellationEligibility> ReadAsync(string? bearer,Guid taxpayerId,Guid invoiceId,CancellationToken ct)
    {
        var binding=await memories.ResolveAsync(bearer,taxpayerId,invoiceId,ct);
        if(binding.Decision!=InvoiceApiDecision.Success)return new(binding.Decision,binding.Code);
        if(binding.Environment==TaxAuthorityEnvironment.Production)return await production.ReadAsync(bearer,taxpayerId,invoiceId,ct);
        if(binding.LegacySandbox)return await sandboxEligibility.ReadAsync(bearer,taxpayerId,invoiceId,ct);
        return new(InvoiceApiDecision.Conflict,"classified_sandbox_legal_successor_not_supported");
    }
}
