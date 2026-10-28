using ArginTaxPayer.Domain.Invoices;
using Microsoft.Extensions.Configuration;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>
/// Single authenticated backend application entrypoint for one new Sandbox dispatch.
/// Every service repeats its own owner and revision checks. Never retries an
/// indeterminate or already-started operation; no second enqueue is allowed.
/// </summary>
public sealed record Phase10OneClickResult(InvoiceApiDecision Decision,string Code,
    string Status="Blocked",string? ReferenceNumber=null,string? InquiryCode=null);

public sealed class Phase10OneClickSandboxSubmissionService(
    Phase10InvoiceDispatchReadService dispatch,
    Phase10LiveSendReadinessService readiness,
    Phase10InternalInvoiceValidationService validation,
    Phase10ReviewReadService reviews,
    InvoiceReviewCommandService reviewCommands,
    Phase10InvoiceFinalizationService finalization,
    SandboxInvoiceFiscalSerialRepository serials,
    Phase10LegacyInvoicePreparationService preparation,
    Phase10DispatchArmService arm,
    IPhase10LegacyOutboxWorker worker,
    Phase10LegacyReferenceInquiryService inquiry,
    IConfiguration configuration)
{
    private static Phase10OneClickResult Stop(string code,
        InvoiceApiDecision decision=InvoiceApiDecision.Conflict)=>
        new(decision,code);

    public async Task<Phase10OneClickResult> SendAsync(
        string? bearer,Guid taxpayerId,Guid invoiceId,CancellationToken ct)
    {
        var current=await dispatch.GetAsync(bearer,taxpayerId,invoiceId,ct);
        if(current.Decision!=InvoiceApiDecision.Success)
            return Stop("invoice_access_denied",current.Decision);
        var state=current.Overview?.Status??"NotPrepared";
        if(state=="Acknowledged")
            return new(InvoiceApiDecision.Success,"already_acknowledged","Acknowledged",
                current.Overview?.ReferenceNumber);
        if(state is not ("NotPrepared" or "Prepared" or "Reserved"))
            return Stop("existing_dispatch_requires_reconciliation");
        if(configuration["SandboxInvoiceLegacy:Enabled"]!="true"||
           configuration["SandboxInvoiceLegacy:OperatorApprovedEndpoint"]!="true")
            return Stop("sandbox_send_disabled",InvoiceApiDecision.Invalid);

        var ready=await readiness.ReadAsync(bearer,taxpayerId,invoiceId,ct);
        if(ready.Decision!=InvoiceApiDecision.Success||ready.Readiness is null)
            return Stop("readiness_unavailable",ready.Decision);
        var info=ready.Readiness;
        if(info.Environment!="Sandbox")
            return Stop("sandbox_required",InvoiceApiDecision.Invalid);
        var revision=info.InvoiceRevision;
        if(info.InvoiceState=="Draft")
        {
            var validated=await validation.ValidateAsync(bearer,taxpayerId,invoiceId,revision,ct);
            if(validated.Decision!=InvoiceApiDecision.Success)
                return Stop(validated.Code,validated.Decision);
            info=(await readiness.ReadAsync(bearer,taxpayerId,invoiceId,ct)).Readiness!;
        }
        if(info.InvoiceState=="Validated")
        {
            var readReview=await reviews.ReadAsync(bearer,taxpayerId,invoiceId,ct);
            if(readReview.Decision!=InvoiceApiDecision.Success||readReview.Review is null)
                return Stop("review_unavailable",readReview.Decision);
            var review=readReview.Review;
            if(review.State is "Draft" or "ChangesRequested")
            {
                var submitted=await reviewCommands.ExecuteAsync(bearer,new(
                    invoiceId,review.Revision,InvoiceReviewAction.SubmitForReview,
                    IdempotencyKey:Guid.NewGuid().ToString("D")),ct);
                if(submitted.Decision!=InvoiceReviewCommandDecision.Success)
                    return Stop("review_submit_failed");
                review=new("InReview",submitted.Revision??(review.Revision+1));
            }
            if(review.State=="InReview")
            {
                var approved=await reviewCommands.ExecuteAsync(bearer,new(
                    invoiceId,review.Revision,InvoiceReviewAction.Approve,
                    IdempotencyKey:Guid.NewGuid().ToString("D")),ct);
                if(approved.Decision!=InvoiceReviewCommandDecision.Success)
                    return Stop("review_approve_failed");
                review=new("Approved",approved.Revision??(review.Revision+1));
            }
            if(review.State!="Approved")return Stop("review_not_approved");
            var finalized=await finalization.FinalizeAsync(bearer,taxpayerId,invoiceId,revision,ct);
            if(finalized.Decision!=InvoiceApiDecision.Success)
                return Stop(finalized.Code,finalized.Decision);
            info=(await readiness.ReadAsync(bearer,taxpayerId,invoiceId,ct)).Readiness!;
        }
        if(info.InvoiceState!="Finalized")return Stop("invoice_not_finalized");

        // Re-check persisted state after every internal transition, before any network send.
        var latest=await dispatch.GetAsync(bearer,taxpayerId,invoiceId,ct);
        state=latest.Overview?.Status??"NotPrepared";
        if(state is not ("NotPrepared" or "Prepared" or "Reserved"))
            return Stop("dispatch_changed_during_preparation");
        if(state=="NotPrepared")
        {
            var serial=await serials.ReserveAsync(bearer,taxpayerId,invoiceId,revision,ct);
            if(serial.Status is not (SandboxSerialStatus.Reserved or SandboxSerialStatus.IdenticalReplay))
                return Stop("serial_"+serial.Status.ToString().ToLowerInvariant());
            var prepared=await preparation.PrepareAsync(bearer,taxpayerId,invoiceId,ct);
            if(prepared.Decision!=InvoiceApiDecision.Success)
                return Stop(prepared.SafeCode,prepared.Decision);
            state="Prepared";
        }
        if(state=="Prepared")
        {
            var armed=await arm.ArmAsync(bearer,taxpayerId,invoiceId,ct);
            if(armed.Decision!=InvoiceApiDecision.Success)
                return Stop(armed.Code,armed.Decision);
        }
        // One send attempt. Worker handles locking and indeterminate outcomes.
        var sent=await worker.ProcessInvoiceAsync(taxpayerId,invoiceId,ct);
        if(sent.Status!="Acknowledged"||string.IsNullOrWhiteSpace(sent.ReferenceNumber))
            return new(InvoiceApiDecision.Conflict,sent.SafeCode,sent.Status);
        string inquiryCode;
        try
        {
            var result=await inquiry.InquireAsync(bearer,taxpayerId,invoiceId,ct);
            inquiryCode=result.Decision==InvoiceApiDecision.Success
                ?result.SafeCode:"inquiry_unavailable";
        }
        catch(Exception)when(!ct.IsCancellationRequested)
        {
            inquiryCode="inquiry_unavailable";
        }
        return new(InvoiceApiDecision.Success,"acknowledged","Acknowledged",
            sent.ReferenceNumber,inquiryCode);
    }
}
