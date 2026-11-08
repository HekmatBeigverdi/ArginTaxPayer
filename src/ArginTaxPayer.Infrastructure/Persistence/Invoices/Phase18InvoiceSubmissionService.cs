using ArginTaxPayer.Application.Invoices;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using Microsoft.Extensions.DependencyInjection;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>
/// Explicit owner-initiated submission. Selects the authority from owned persisted
/// invoice memory; previously attempted operations always require reconciliation.
/// </summary>
public sealed class Phase18InvoiceSubmissionService(
    Phase18InvoiceMemoryBindingService memories,
    Phase10OneClickSandboxSubmissionService sandbox,
    InvoiceSubscriptionGate subscriptions,
    Phase10InvoiceDispatchReadService dispatch,
    Phase10InternalInvoiceValidationService validation,
    Phase10ReviewReadService reviews,
    InvoiceReviewCommandService reviewCommands,
    Phase10InvoiceFinalizationService finalization,
    IServiceProvider services)
{
    public async Task<Phase10OneClickResult> SendAsync(
        string? bearer, Guid taxpayerId, Guid invoiceId, CancellationToken ct)
    {
        var binding = await memories.ResolveAsync(bearer, taxpayerId, invoiceId, ct);
        if (binding.Decision != InvoiceApiDecision.Success)
            return new(binding.Decision, binding.Code);
        var environment = binding.Environment.ToString();
        if (binding.LegacySandbox)
            return (await sandbox.SendAsync(bearer, taxpayerId, invoiceId, ct)) with { Environment = environment };
        Phase10OneClickResult Stop(string code, InvoiceApiDecision decision = InvoiceApiDecision.Conflict) =>
            new(decision, code, Environment: environment);
        var current = await dispatch.GetAsync(bearer, taxpayerId, invoiceId, ct);
        if (current.Decision != InvoiceApiDecision.Success)
            return Stop("invoice_access_denied", current.Decision);
        if (current.Overview is { } overview)
        {
            if (overview.Environment != environment)
                return Stop("operation_environment_mismatch");
            if (ExistingDispatchResult(overview) is { } existing) return existing;
        }
        var subscriptionError = await subscriptions.CheckAsync(binding.Owner, ct);
        if (subscriptionError is not null) return Stop(subscriptionError);
        var revision = binding.Revision;
        var state = binding.State;
        if (state == InvoiceLifecycleState.Draft)
        {
            var validated = await validation.ValidateAsync(bearer, taxpayerId, invoiceId, revision, ct);
            if (validated.Decision != InvoiceApiDecision.Success) return Stop(validated.Code, validated.Decision);
            state = InvoiceLifecycleState.Validated;
        }
        if (state == InvoiceLifecycleState.Validated)
        {
            var read = await reviews.ReadAsync(bearer, taxpayerId, invoiceId, ct);
            if (read.Decision != InvoiceApiDecision.Success || read.Review is null)
                return Stop("review_unavailable", read.Decision);
            var review = read.Review;
            if (review.State is "Draft" or "ChangesRequested")
            {
                var submitted = await reviewCommands.ExecuteAsync(bearer, new(
                    invoiceId, review.Revision, InvoiceReviewAction.SubmitForReview,
                    IdempotencyKey: Guid.NewGuid().ToString("D")), ct);
                if (submitted.Decision != InvoiceReviewCommandDecision.Success) return Stop("review_submit_failed");
                review = new("InReview", submitted.Revision ?? review.Revision + 1);
            }
            if (review.State == "InReview")
            {
                var approved = await reviewCommands.ExecuteAsync(bearer, new(
                    invoiceId, review.Revision, InvoiceReviewAction.Approve,
                    IdempotencyKey: Guid.NewGuid().ToString("D")), ct);
                if (approved.Decision != InvoiceReviewCommandDecision.Success) return Stop("review_approve_failed");
                review = new("Approved", approved.Revision ?? review.Revision + 1);
            }
            if (review.State != "Approved") return Stop("review_not_approved");
            var finalized = await finalization.FinalizeAsync(bearer, taxpayerId, invoiceId, revision, ct);
            if (finalized.Decision != InvoiceApiDecision.Success) return Stop(finalized.Code, finalized.Decision);
            state = InvoiceLifecycleState.Finalized;
        }
        if (state != InvoiceLifecycleState.Finalized) return Stop("invoice_not_finalized");
        // Recheck after internal transitions; preparation repeats ownership,
        // review and immutable snapshot checks before creating a reserved operation.
        var latest = await dispatch.GetAsync(bearer, taxpayerId, invoiceId, ct);
        if (latest.Decision != InvoiceApiDecision.Success) return Stop("invoice_access_denied", latest.Decision);
        if (latest.Overview is { } changed)
        {
            if (changed.Environment != environment) return Stop("operation_environment_mismatch");
            if (ExistingDispatchResult(changed) is { } existing) return existing;
        }
        var prepared = await services.GetRequiredService<Phase18ProductionInvoicePreparationService>()
            .PrepareAsync(bearer, taxpayerId, invoiceId, ct);
        if (prepared.Decision != InvoiceApiDecision.Success) return Stop(prepared.SafeCode, prepared.Decision);
        if (prepared.OperationId is not { } operationId) return Stop("prepared_operation_missing");
        var sent = await services.GetRequiredService<Phase18ScopedLegacyOutboxWorker>().ProcessAsync(
            operationId, binding.Owner, taxpayerId, invoiceId, binding.Environment, ct);
        return new(sent.Status == "Acknowledged" && !string.IsNullOrWhiteSpace(sent.ReferenceNumber)
                ? InvoiceApiDecision.Success : InvoiceApiDecision.Conflict,
            sent.SafeCode, sent.Status, sent.ReferenceNumber, Environment: environment);
    }

    internal static Phase10OneClickResult? ExistingDispatchResult(InvoiceDispatchOverview overview) =>
        overview.Status switch
        {
            "Acknowledged" when !string.IsNullOrWhiteSpace(overview.ReferenceNumber) =>
                new(InvoiceApiDecision.Success, "already_acknowledged", overview.Status,
                    overview.ReferenceNumber, Environment: overview.Environment),
            "NotPrepared" or "Prepared" or "Reserved" => null,
            _ => new(InvoiceApiDecision.Conflict, "existing_dispatch_requires_reconciliation",
                overview.Status, Environment: overview.Environment)
        };
}
