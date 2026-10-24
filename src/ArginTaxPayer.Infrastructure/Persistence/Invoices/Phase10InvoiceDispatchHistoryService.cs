using System.Data;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>
/// Scoped, read-only projection of persisted invoice dispatch evidence.
/// NEVER returns signed packet, private key, raw authority JSON, token,
/// full invoice fields, hash of payload, or any authority-backed assertion.
/// </summary>
public sealed record Phase10DispatchAttemptView(
    int Number, DateTime StartedAtUtc, DateTime? EndedAtUtc, string Result);
public sealed record Phase10DispatchReceiptView(
    string Kind, DateTime ObservedAtUtc, bool HasAuthorityReference);
public sealed record Phase10DispatchHistoryView(
    string Environment, bool SubmissionEnabled, bool FiscalAccepted,
    bool SignedPreviewAvailable, long? InvoiceRevision, string State,
    DateTime? LastUpdatedAtUtc,
    Phase10DispatchAttemptView[] Attempts, Phase10DispatchReceiptView[] Receipts,
    string RecoveryAction = "ManualReview", string RecoveryCode = "invoice.recovery.unrecognized_stop",
    bool MayRetry = false, bool MayInquire = false);
public sealed record Phase10DispatchHistoryResult(
    InvoiceApiDecision Decision, Phase10DispatchHistoryView? Value = null);

public sealed class Phase10InvoiceDispatchHistoryService(
    ArginTaxPayerDbContext db, TaxpayerScopeService scope)
{
    public async Task<Phase10DispatchHistoryResult> ReadAsync(
        string? bearer,Guid taxpayerId,Guid invoiceId,CancellationToken ct=default)
    {
        if(taxpayerId==Guid.Empty||invoiceId==Guid.Empty)
            return new(InvoiceApiDecision.Invalid);
        var actor=await scope.ResolveAsync(bearer,ct);
        if(actor.Decision==TaxpayerScopeDecision.Unauthenticated)
            return new(InvoiceApiDecision.Unauthenticated);
        if(actor.Decision!=TaxpayerScopeDecision.Allowed)
            return new(InvoiceApiDecision.Forbidden);
        var owner=await scope.ScopedQuery(actor.ActorId,actor.Level1)
            .Where(x=>x.Id==taxpayerId).Select(x=>x.CustomerUserId)
            .SingleOrDefaultAsync(ct);
        if(owner is null||owner.Value==Guid.Empty)
            return new(InvoiceApiDecision.NotFound);
        if(!await db.InvoiceDocuments.AsNoTracking().AnyAsync(x=>
            x.Id==invoiceId&&x.TaxpayerId==taxpayerId&&
            x.CustomerUserId==owner.Value,ct))
            return new(InvoiceApiDecision.NotFound);

        var conn=db.Database.GetDbConnection();
        var opened=conn.State!=ConnectionState.Open;
        if(opened)await conn.OpenAsync(ct);
        try {
            Guid operationId=Guid.Empty;
            long? revision=null;
            DateTime? updated=null;
            var state="NotPrepared";
            await using(var command=conn.CreateCommand()){
                command.CommandText="""
SELECT id, invoice_revision, state, updated_at_utc
FROM invoice_dispatch_operations
WHERE customer_user_id=@owner AND taxpayer_id=@taxpayer AND invoice_id=@invoice
ORDER BY invoice_revision DESC, created_at_utc DESC, id
LIMIT 1
""";
                Add(command,"owner",owner.Value);Add(command,"taxpayer",taxpayerId);
                Add(command,"invoice",invoiceId);
                await using var reader=await command.ExecuteReaderAsync(ct);
                if(await reader.ReadAsync(ct)){
                    operationId=reader.GetGuid(0);
                    revision=reader.GetInt64(1);
                    var number=reader.GetInt32(2);
                    state=Enum.IsDefined(typeof(ArginTaxPayer.Domain.Invoices.InvoiceDispatchState),number)
                        ?((ArginTaxPayer.Domain.Invoices.InvoiceDispatchState)number).ToString()
                        :"Unrecognized";
                    updated=reader.GetDateTime(3);
                }
            }
            var attempts=new List<Phase10DispatchAttemptView>();
            var receipts=new List<Phase10DispatchReceiptView>();
            if(operationId!=Guid.Empty){
                await using(var cmd=conn.CreateCommand()){
                    cmd.CommandText="""
SELECT attempt_number,started_at_utc,ended_at_utc,result_code
FROM invoice_dispatch_attempts
WHERE operation_id=@operation
ORDER BY attempt_number DESC LIMIT 30
""";
                    Add(cmd,"operation",operationId);
                    await using var reader=await cmd.ExecuteReaderAsync(ct);
                    while(await reader.ReadAsync(ct)){
                        var code=reader.IsDBNull(3)?"Pending":reader.GetString(3);
                        // Provider response codes can be uncontrolled. No raw arbitrary
                        // string crosses the API; use only a short semantic class.
                        var safe=code is "success" or "acknowledged" or "failed"
                            or "timeout" or "indeterminate" ? code : "Recorded";
                        attempts.Add(new(reader.GetInt32(0),reader.GetDateTime(1),
                            reader.IsDBNull(2)?null:reader.GetDateTime(2),safe));
                    }
                }
                await using(var cmd=conn.CreateCommand()){
                    cmd.CommandText="""
SELECT receipt_kind,observed_at_utc,authority_reference
FROM invoice_dispatch_receipts
WHERE operation_id=@operation
ORDER BY observed_at_utc DESC,id DESC LIMIT 30
""";
                    Add(cmd,"operation",operationId);
                    await using var reader=await cmd.ExecuteReaderAsync(ct);
                    while(await reader.ReadAsync(ct))
                        receipts.Add(new(reader.GetInt32(0)==1?"InitialReceipt":"InquiryEvidence",
                            reader.GetDateTime(1),!reader.IsDBNull(2)));
                }
            }
            // Signed packet bytes are deliberately not read from DB or reconstructed.
            // Even a Signed state does not establish a previewable signed artifact.
            var next=ArginTaxPayer.Application.Invoices.Phase10InvoiceRecoveryAdvice
                .ForState(state,false);
            return new(InvoiceApiDecision.Success,new("Mock",false,false,false,
                revision,state,updated,attempts.ToArray(),receipts.ToArray(),
                next.NextAction.ToString(),next.SafeCode,next.MayRetry,next.MayInquire));
        }finally{if(opened)await conn.CloseAsync();}
    }

    private static void Add(System.Data.Common.DbCommand cmd,string name,object value){
        var p=cmd.CreateParameter();p.ParameterName=name;p.Value=value;cmd.Parameters.Add(p);
    }
}
