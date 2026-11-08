using ArginTaxPayer.Domain.Invoices;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public sealed class Phase18SalesReturnServiceRouter(
    Phase18InvoiceMemoryBindingService memories,
    Phase18ProductionSalesReturnService production,
    Phase10SandboxSalesReturnService sandbox)
{
    public Task<SalesReturnOperationResult> OpenAsync(string? b,Guid t,Guid i,CancellationToken ct)=>Route(b,t,i,ct,(p,s)=>p.OpenAsync(b,t,i,ct),(p,s)=>s.OpenAsync(b,t,i,ct));
    public Task<SalesReturnOperationResult> ReadAsync(string? b,Guid t,Guid i,CancellationToken ct)=>Route(b,t,i,ct,(p,s)=>p.ReadAsync(b,t,i,ct),(p,s)=>s.ReadAsync(b,t,i,ct));
    public Task<SalesReturnOperationResult> SaveAsync(string? b,Guid t,Guid i,SalesReturnSaveRequest input,CancellationToken ct)=>Route(b,t,i,ct,(p,s)=>p.SaveAsync(b,t,i,input,ct),(p,s)=>s.SaveAsync(b,t,i,input,ct));
    public Task<SalesReturnOperationResult> DiscardAsync(string? b,Guid t,Guid i,CancellationToken ct)=>Route(b,t,i,ct,(p,s)=>p.DiscardAsync(b,t,i,ct),(p,s)=>s.DiscardAsync(b,t,i,ct));
    public Task<SalesReturnOperationResult> SubmitAsync(string? b,Guid t,Guid i,CancellationToken ct)=>Route(b,t,i,ct,(p,s)=>p.SubmitAsync(b,t,i,ct),(p,s)=>s.SubmitAsync(b,t,i,ct));
    private async Task<SalesReturnOperationResult> Route(string? b,Guid t,Guid i,CancellationToken ct,
        Func<Phase18ProductionSalesReturnService,Phase10SandboxSalesReturnService,Task<SalesReturnOperationResult>> prod,
        Func<Phase18ProductionSalesReturnService,Phase10SandboxSalesReturnService,Task<SalesReturnOperationResult>> sand)
    {
        var binding=await memories.ResolveAsync(b,t,i,ct);
        if(binding.Decision!=InvoiceApiDecision.Success)return new(binding.Decision,binding.Code);
        if(binding.Environment==TaxAuthorityEnvironment.Production)return await prod(production,sandbox);
        if(binding.LegacySandbox)return await sand(production,sandbox);
        return new(InvoiceApiDecision.Conflict,"classified_sandbox_legal_successor_not_supported");
    }
}
