using ArginTaxPayer.Domain.Invoices;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>Selects the legal-successor implementation only from the invoice's persisted memory binding.</summary>
public sealed class Phase18CorrectionServiceRouter(
    Phase18InvoiceMemoryBindingService memories,
    Phase18ProductionCorrectionService production,
    Phase10SandboxCorrectionService sandbox)
{
    public Task<CorrectionOperationResult> OpenAsync(string? bearer, Guid taxpayerId, Guid invoiceId, CancellationToken ct) =>
        Route(bearer,taxpayerId,invoiceId,ct,(p,s)=>p.OpenAsync(bearer,taxpayerId,invoiceId,ct),(p,s)=>s.OpenAsync(bearer,taxpayerId,invoiceId,ct));
    public Task<CorrectionOperationResult> ReadAsync(string? bearer, Guid taxpayerId, Guid invoiceId, CancellationToken ct) =>
        Route(bearer,taxpayerId,invoiceId,ct,(p,s)=>p.ReadAsync(bearer,taxpayerId,invoiceId,ct),(p,s)=>s.ReadAsync(bearer,taxpayerId,invoiceId,ct));
    public Task<CorrectionOperationResult> SaveAsync(string? bearer, Guid taxpayerId, Guid invoiceId, CorrectionSaveRequest input, CancellationToken ct) =>
        Route(bearer,taxpayerId,invoiceId,ct,(p,s)=>p.SaveAsync(bearer,taxpayerId,invoiceId,input,ct),(p,s)=>s.SaveAsync(bearer,taxpayerId,invoiceId,input,ct));
    public Task<CorrectionOperationResult> DiscardAsync(string? bearer, Guid taxpayerId, Guid invoiceId, CancellationToken ct) =>
        Route(bearer,taxpayerId,invoiceId,ct,(p,s)=>p.DiscardAsync(bearer,taxpayerId,invoiceId,ct),(p,s)=>s.DiscardAsync(bearer,taxpayerId,invoiceId,ct));
    public Task<CorrectionOperationResult> SubmitAsync(string? bearer, Guid taxpayerId, Guid invoiceId, CancellationToken ct) =>
        Route(bearer,taxpayerId,invoiceId,ct,(p,s)=>p.SubmitAsync(bearer,taxpayerId,invoiceId,ct),(p,s)=>s.SubmitAsync(bearer,taxpayerId,invoiceId,ct));

    private async Task<CorrectionOperationResult> Route(string? bearer,Guid taxpayerId,Guid invoiceId,
        CancellationToken ct,Func<Phase18ProductionCorrectionService,Phase10SandboxCorrectionService,Task<CorrectionOperationResult>> prod,
        Func<Phase18ProductionCorrectionService,Phase10SandboxCorrectionService,Task<CorrectionOperationResult>> sand)
    {
        var binding=await memories.ResolveAsync(bearer,taxpayerId,invoiceId,ct);
        if(binding.Decision!=InvoiceApiDecision.Success)return new(binding.Decision,binding.Code);
        if(binding.Environment==TaxAuthorityEnvironment.Production)return await prod(production,sandbox);
        if(binding.LegacySandbox)return await sand(production,sandbox);
        return new(InvoiceApiDecision.Conflict,"classified_sandbox_legal_successor_not_supported");
    }
}
