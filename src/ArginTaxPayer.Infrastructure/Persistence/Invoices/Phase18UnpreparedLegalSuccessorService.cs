using ArginTaxPayer.Domain.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>Explicitly resumes the exact frozen legal document; never creates a replacement.</summary>
public sealed class Phase18UnpreparedLegalSuccessorService(
    ArginTaxPayerDbContext db, Phase18InvoiceMemoryBindingService memories,
    Phase18InvoiceSubmissionService sender, IConfiguration configuration)
{
    public async Task<CancellationSendResult> ResumeAsync(string? bearer,Guid taxpayerId,Guid invoiceId,CancellationToken ct)
    {
        if(configuration["Phase18Production:LegalSuccessorsEnabled"]!="true" ||
            configuration["Phase18Production:LegalSuccessorsOperatorApproved"]!="true")
            return new(InvoiceApiDecision.Conflict,"production_legal_successors_disabled");
        var binding=await memories.ResolveAsync(bearer,taxpayerId,invoiceId,ct);
        if(binding.Decision!=InvoiceApiDecision.Success)return new(binding.Decision,binding.Code);
        var document=await db.InvoiceDocuments.AsNoTracking().Where(d=>d.Id==invoiceId &&
            d.TaxpayerId==taxpayerId && d.CustomerUserId==binding.Owner)
            .Select(d=>new {d.Subject,d.State}).SingleOrDefaultAsync(ct);
        if(document is null)return new(InvoiceApiDecision.NotFound,"invoice_not_found");
        var active=await db.Database.SqlQuery<int>($"""
SELECT 1 AS "Value" FROM authority_invoice_legal_relations
WHERE successor_invoice_id={invoiceId} AND customer_user_id={binding.Owner}
 AND taxpayer_id={taxpayerId} AND subject={(int)document.Subject} AND state=1
LIMIT 1
""").AnyAsync(ct);
        var dispatched=await db.Database.SqlQuery<int>($"""
SELECT 1 AS "Value" FROM invoice_dispatch_operations
WHERE invoice_id={invoiceId} AND customer_user_id={binding.Owner} AND taxpayer_id={taxpayerId} LIMIT 1
""").AnyAsync(ct);
        var reserved=await db.Database.SqlQuery<int>($"""
SELECT 1 AS "Value" FROM authority_invoice_fiscal_reservations
WHERE invoice_id={invoiceId} AND customer_user_id={binding.Owner} AND taxpayer_id={taxpayerId} LIMIT 1
""").AnyAsync(ct);
        if(!CanResume(document.Subject,document.State,binding.Environment,active,dispatched,reserved))
            return new(InvoiceApiDecision.Conflict,"legal_successor_requires_reconciliation",invoiceId);
        // The existing sender rechecks scope, subscription, immutable revision,
        // accepted predecessor, exact memory and the durable single-attempt gate.
        var result=await sender.SendAsync(bearer,taxpayerId,invoiceId,ct);
        return new(result.Decision,result.Code,invoiceId,result.Status,result.ReferenceNumber,result.InquiryCode);
    }

    internal static bool CanResume(InvoiceSubject subject,InvoiceLifecycleState state,
        TaxAuthorityEnvironment environment,bool activeRelation,bool hasDispatch,bool hasReservation) =>
        subject is InvoiceSubject.Correction or InvoiceSubject.Cancellation or InvoiceSubject.SalesReturn &&
        state==InvoiceLifecycleState.Finalized && environment==TaxAuthorityEnvironment.Production &&
        activeRelation && !hasDispatch && !hasReservation;
}
