using ArginTaxPayer.Application.Invoices;
using ArginTaxPayer.Domain.Invoices;
using Microsoft.Extensions.DependencyInjection;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public sealed record Phase18InvoiceInquiryResult(InvoiceApiDecision Decision,
    string SafeCode, string? ReferenceNumber = null,
    LegacyAuthorityInquiryReport? AuthorityResult = null, string? Environment = null);

/// <summary>Authenticated read-only inquiry for the persisted invoice authority. Never resends.</summary>
public sealed class Phase18InvoiceInquiryService(
    Phase18InvoiceMemoryBindingService memories, Phase10InvoiceDispatchReadService dispatch,
    Phase10LegacyReferenceInquiryService sandbox, IServiceProvider services,
    Phase18ProductionAuthorityReportRepository productionReports)
{
    public Task<Phase18InvoiceInquiryResult> ReadAsync(string? bearer, Guid taxpayerId,
        Guid invoiceId, CancellationToken ct) => ExecuteAsync(bearer, taxpayerId, invoiceId, false, ct);

    public Task<Phase18InvoiceInquiryResult> InquireAsync(string? bearer, Guid taxpayerId,
        Guid invoiceId, CancellationToken ct) => ExecuteAsync(bearer, taxpayerId, invoiceId, true, ct);

    private async Task<Phase18InvoiceInquiryResult> ExecuteAsync(string? bearer,
        Guid taxpayerId, Guid invoiceId, bool inquire, CancellationToken ct)
    {
        var binding = await memories.ResolveAsync(bearer, taxpayerId, invoiceId, ct);
        if (binding.Decision != InvoiceApiDecision.Success) return new(binding.Decision, binding.Code);
        var environment = binding.Environment.ToString();
        if (binding.LegacySandbox)
        {
            var result = inquire
                ? await sandbox.InquireAsync(bearer, taxpayerId, invoiceId, ct)
                : await sandbox.ReadAsync(bearer, taxpayerId, invoiceId, ct);
            return new(result.Decision, result.SafeCode, result.ReferenceNumber, result.AuthorityResult, environment);
        }
        var current = await dispatch.GetAsync(bearer, taxpayerId, invoiceId, ct);
        if (current.Decision != InvoiceApiDecision.Success)
            return new(current.Decision, "invoice_access_denied", Environment: environment);
        if (current.Overview is not { Status: "Acknowledged", ReferenceNumber: not null } operation ||
            operation.Environment != environment)
            return new(InvoiceApiDecision.Conflict, "acknowledged_reference_not_found", Environment: environment);
        if (!inquire)
        {
            var saved=await productionReports.ReadAsync(binding.Owner,taxpayerId,invoiceId,
                operation.OperationId,operation.ReferenceNumber,ct);
            return new(InvoiceApiDecision.Success,
                saved is null?"acknowledged_pending_inquiry":"inquiry_report_loaded",
                operation.ReferenceNumber,saved,environment);
        }
        var outcome = await services.GetRequiredService<Phase18ScopedLegacyReferenceInquiryService>()
            .InquireAsync(operation.OperationId, binding.Owner, taxpayerId, invoiceId,
                binding.Environment, ct);
        // Surface the bounded authority report like Sandbox. Status remains
        // informational; this response does not declare fiscal acceptance.
        return new(InvoiceApiDecision.Success, outcome.Code, outcome.ReferenceNumber,
            outcome.AuthorityResult, environment);
    }
}
