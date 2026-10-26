using System.Data;
using ArginTaxPayer.Application.Invoices;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>Claims an existing, signed legacy Outbox operation for explicit Sandbox dispatch.
/// Never performs external I/O or modifies the signed payload.</summary>
public sealed class Phase10DispatchArmService(ArginTaxPayerDbContext db,
    Phase10InvoiceDispatchReadService reader)
{
    public async Task<(InvoiceApiDecision Decision, string Code)> ArmAsync(
        string? bearer, Guid taxpayerId, Guid invoiceId, CancellationToken ct)
    {
        var scoped=await reader.GetAsync(bearer,taxpayerId,invoiceId,ct);
        if(scoped.Decision!=InvoiceApiDecision.Success)
            return (scoped.Decision,"scope_denied");
        if(scoped.Overview is null)
            return (InvoiceApiDecision.NotFound,"dispatch_not_prepared");
        if(scoped.Overview.Status=="Reserved")
            return (InvoiceApiDecision.Success,"already_reserved");
        if(scoped.Overview.Status!="Prepared")
            return (InvoiceApiDecision.Conflict,"dispatch_not_prepared");

        var conn=db.Database.GetDbConnection();
        var opened=conn.State!=ConnectionState.Open;
        if(opened) await conn.OpenAsync(ct);
        try
        {
            await using var tx=await conn.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            try
            {
                await using(var guard=conn.CreateCommand())
                {
                    guard.Transaction=tx;
                    guard.CommandText="SELECT current_database()";
                    if((string?)await guard.ExecuteScalarAsync(ct)!="argintaxpayer_test")
                        throw new InvalidOperationException("Sandbox arm requires isolated test DB.");
                }
                await using var cmd=conn.CreateCommand();
                cmd.Transaction=tx;
                cmd.CommandText="""
UPDATE invoice_dispatch_operations d
SET state=3, state_revision=state_revision+2, updated_at_utc=now()
FROM invoice_documents i, outbox_messages o
WHERE d.id=@operation AND d.invoice_id=@invoice AND d.taxpayer_id=@taxpayer
  AND d.state=1 AND d.state_revision=@revision
  AND i.id=d.invoice_id AND i.taxpayer_id=d.taxpayer_id
  AND i.customer_user_id=d.customer_user_id AND i.revision=d.invoice_revision
  AND i.state=3
  AND o.id=d.id AND o.taxpayer_id=d.taxpayer_id
  AND o.event_type='invoice.sandbox.legacy.dispatch.requested'
  AND o.processed_at_utc IS NULL AND o.attempt_count=0
  AND o.payload_json::jsonb->>'operationId'=d.id::text
  AND o.payload_json::jsonb->>'invoiceId'=d.invoice_id::text
  AND o.payload_json::jsonb->>'customerUserId'=d.customer_user_id::text
  AND o.payload_json::jsonb->>'environment'='Sandbox'
  AND o.payload_json::jsonb->>'requestSha256'=d.payload_sha256
  AND NOT EXISTS(SELECT 1 FROM invoice_dispatch_attempts a WHERE a.operation_id=d.id)
RETURNING d.id
""";
                foreach(var (name,value) in new (string,object)[] {
                    ("operation",scoped.Overview.OperationId),
                    ("invoice",invoiceId),("taxpayer",taxpayerId),
                    ("revision",scoped.Overview.StateRevision)
                })
                {
                    var parameter=cmd.CreateParameter();
                    parameter.ParameterName=name;parameter.Value=value;cmd.Parameters.Add(parameter);
                }
                var changed=await cmd.ExecuteScalarAsync(ct);
                if(changed is null)
                    return (InvoiceApiDecision.Conflict,"dispatch_arm_conflict");
                await tx.CommitAsync(ct);
                return (InvoiceApiDecision.Success,"dispatch_reserved");
            }
            catch
            {
                await tx.RollbackAsync(CancellationToken.None);
                throw;
            }
        }
        finally { if(opened)await conn.CloseAsync(); }
    }
}
