using System.Data;
using System.Data.Common;
using System.Text.Json;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Parties;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using ArginTaxPayer.Infrastructure.Signing.Connection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public sealed record Phase10LegacyPreparationResult(
    InvoiceApiDecision Decision, string SafeCode, Guid? OperationId=null);

/// <summary>
/// One finalized Sandbox invoice -> immutable official reference projection ->
/// Legacy PKCS8 signature -> operation and outbox in ONE database transaction.
/// This does not transmit a tax invoice. Worker sends only a committed event.
/// The scope is the already-validated Type 1 / Pattern 1 reference profile.
/// </summary>
public sealed class Phase10LegacyInvoicePreparationService(
    ArginTaxPayerDbContext db, TaxpayerScopeService scope, IServiceProvider services)
{
    public async Task<Phase10LegacyPreparationResult> PrepareAsync(
        string? bearer, Guid taxpayerId, Guid invoiceId,
        CancellationToken ct=default)
    {
        if(taxpayerId==Guid.Empty || invoiceId==Guid.Empty)
            return Denied("invalid_invoice");
        var actor=await scope.ResolveAsync(bearer,ct);
        if(actor.Decision==TaxpayerScopeDecision.Unauthenticated)
            return new(InvoiceApiDecision.Unauthenticated,"unauthenticated");
        if(actor.Decision!=TaxpayerScopeDecision.Allowed)
            return new(InvoiceApiDecision.Forbidden,"forbidden");
        var owner=await scope.ScopedQuery(actor.ActorId,actor.Level1)
            .Where(t=>t.Id==taxpayerId).Select(t=>t.CustomerUserId)
            .SingleOrDefaultAsync(ct);
        if(owner is null || owner==Guid.Empty)
            return new(InvoiceApiDecision.NotFound,"invoice_not_found");
        if(!await db.CustomerTaxpayerMemberships.AsNoTracking().AnyAsync(m=>
                m.CustomerUserId==owner.Value && m.TaxpayerId==taxpayerId &&
                m.UnlinkedAtUtc==null,ct))
            return new(InvoiceApiDecision.Forbidden,"membership_inactive");

        var invoice=await db.InvoiceDocuments.AsNoTracking()
            .SingleOrDefaultAsync(i=>i.Id==invoiceId && i.CustomerUserId==owner.Value &&
                i.TaxpayerId==taxpayerId,ct);
        if(invoice is null)
            return new(InvoiceApiDecision.NotFound,"invoice_not_found");
        if(invoice.State!=InvoiceLifecycleState.Finalized)
            return Denied("invoice_not_finalized");
        if(!await db.InvoiceReviews.AsNoTracking().AnyAsync(r=>r.InvoiceId==invoiceId &&
                r.CustomerUserId==owner.Value && r.TaxpayerId==taxpayerId &&
                r.State==InvoiceReviewState.Approved,ct))
            return Denied("invoice_review_not_approved");
        if(!await db.InvoiceDocumentRevisions.AsNoTracking().AnyAsync(r=>
                r.InvoiceId==invoiceId && r.Revision==invoice.Revision &&
                r.CustomerUserId==owner.Value && r.TaxpayerId==taxpayerId &&
                r.SnapshotSha256==invoice.SnapshotSha256 &&
                r.SnapshotJson==invoice.SnapshotJson,ct))
            return Denied("immutable_snapshot_not_verified");

        JsonElement root;
        using(var document=JsonDocument.Parse(invoice.SnapshotJson))
            root=document.RootElement.Clone();
        if(!root.TryGetProperty("environment",out var env)||
           env.GetString()!="Sandbox" ||
           !root.TryGetProperty("sandboxFiscalMemoryId",out var memoryField) ||
           !memoryField.TryGetGuid(out var memoryId) || memoryId==Guid.Empty ||
           !root.TryGetProperty("buyerPartyId",out var buyerField) ||
           !buyerField.TryGetGuid(out var buyerId) || buyerId==Guid.Empty ||
           !root.TryGetProperty("lines",out var linesField))
            return Denied("sandbox_snapshot_incomplete");

        InvoiceEntryLine[] lines;
        try { lines=JsonSerializer.Deserialize<InvoiceEntryLine[]>(linesField.GetRawText())??[]; }
        catch(JsonException){return Denied("invoice_lines_invalid");}
        if(lines.Length==0 || lines.Any(l=>l.CatalogItemId is null ||
                l.UnitId is null || l.OfficialIdentifierSnapshot is null))
            return Denied("official_invoice_lines_incomplete");

        var seller=await db.TaxpayerProfiles.AsNoTracking()
            .Where(t=>t.Id==taxpayerId && t.CustomerUserId==owner.Value)
            .Select(t=>t.NormalizedIdentifier).SingleOrDefaultAsync(ct);
        var buyer=await (from link in db.CustomerPartyLinks.AsNoTracking()
            join party in db.Parties.AsNoTracking() on link.PartyId equals party.Id
            where link.CustomerUserId==owner.Value && link.PartyId==buyerId &&
                link.UnlinkedAtUtc==null && party.Status==PartyStatus.Active
            select party.NormalizedIdentifier).SingleOrDefaultAsync(ct);
        if(string.IsNullOrWhiteSpace(seller) || string.IsNullOrWhiteSpace(buyer))
            return Denied("seller_or_buyer_identity_missing");

        var itemIds=lines.Select(l=>l.CatalogItemId!.Value).Distinct().ToArray();
        var mapping=await db.CatalogOfficialMappings.AsNoTracking()
            .Where(m=>itemIds.Contains(m.ItemId) &&
                m.EffectiveFromUtc<=invoice.IssueDateUtc &&
                (m.EffectiveToUtc==null || m.EffectiveToUtc>invoice.IssueDateUtc))
            .Join(db.OfficialGoodsServiceEntries.AsNoTracking(),
                m=>m.OfficialEntryId,e=>e.Id,(m,e)=>new{m.ItemId,e.Code,
                    e.VerificationStatus,e.ReleaseId})
            .Where(m=>m.VerificationStatus==2 &&
                db.CatalogReferenceReleases.Any(r=>r.Id==m.ReleaseId && r.State==3))
            .ToArrayAsync(ct);
        if(lines.Any(l=>mapping.Where(m=>m.ItemId==l.CatalogItemId)
                 .Select(m=>m.Code).Distinct().Count()!=1 ||
             !mapping.Any(m=>m.ItemId==l.CatalogItemId &&
                 m.Code==l.OfficialIdentifierSnapshot)))
            return Denied("official_goods_mapping_invalid");

        var unitIds=lines.Select(l=>l.UnitId!.Value).Distinct().ToArray();
        var units=await db.OfficialMeasurementUnits.AsNoTracking()
            .Where(u=>unitIds.Contains(u.Id) && u.Status==1 &&
                db.CatalogReferenceReleases.Any(r=>r.Id==u.ReleaseId && r.State==3))
            .Select(u=>new{u.Id,u.Code}).ToDictionaryAsync(u=>u.Id,u=>u.Code,ct);
        if(units.Count!=unitIds.Length)
            return Denied("official_units_unverified");

        var reservation=await ReadReservationAsync(owner.Value,taxpayerId,invoiceId,
            invoice.Revision,memoryId,invoice.SnapshotSha256,ct);
        if(reservation is null)
            return Denied("fiscal_serial_not_reserved");
        var memoryCode=await ReadMemoryCodeAsync(owner.Value,taxpayerId,memoryId,ct);
        if(memoryCode is not {Length:6})
            return Denied("sandbox_memory_not_ready");
        var invoiceNumber=await ReadInvoiceNumberAsync(invoiceId,ct);
        if(string.IsNullOrWhiteSpace(invoiceNumber))
            return Denied("invoice_number_missing");

        InvoiceWireProjection wire;
        try {
            wire=Phase10LegacyFrozenInvoiceProjector.Project(invoice,
                reservation,seller,buyer,units,invoiceNumber);
        }
        catch(Exception exception) when(exception is ArgumentException or
            InvalidOperationException or NotSupportedException or OverflowException or
            KeyNotFoundException or JsonException)
        {
            return Denied("legacy_wire_mapping_unverified");
        }

        // Idempotent read-only fast path: do not produce a fresh signed package
        // when the same revision already has a durable outbox operation.
        var existing=await FindExistingAsync(owner.Value,taxpayerId,invoiceId,invoice.Revision,ct);
        if(existing is { } found)
            return found.Prepared
                ? new(InvoiceApiDecision.Success,"already_prepared",found.Id)
                : Denied("dispatch_operation_requires_reconciliation");

        var loader=services.GetRequiredService<SandboxLegacySigningKeyLoader>();
        using var key=await loader.RecoverAsync(owner.Value,taxpayerId,memoryId,ct);
        if(key is null)return Denied("sandbox_legacy_key_unavailable");
        var auth=services.GetRequiredService<LegacyInvoiceAccessTokenClient>();
        var token=await auth.RequestAsync(memoryCode,key.PrivateKey,key.PublicSpkiDer,ct);
        if(!token.Authenticated || token.AccessToken is null)
            return Denied(token.SafeCode);

        var operationId=Guid.NewGuid();
        var trace=Guid.NewGuid();
        var timestamp=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
        var encryptionClient=services.GetRequiredService<LegacySandboxServerEncryptionKeyClient>();
        using var authorityKey=await encryptionClient.RequestAsync(
            memoryCode,key.PrivateKey,key.PublicSpkiDer,ct);
        if(authorityKey is null)
            return Denied("sandbox_legacy_authority_encryption_key_unavailable");
        var packet=LegacyInvoiceSubmissionPacketBuilder.BuildEncrypted(operationId,
            owner.Value,taxpayerId,reservation,wire.Json,key.PrivateKey,
            key.PublicSpkiDer,token.AccessToken,trace,timestamp,memoryCode,authorityKey);
        var outboxPayload=JsonSerializer.Serialize(new {
            schema="phase10.sandbox.legacy.invoice.dispatch.v1",
            environment="Sandbox",operationId,customerUserId=owner.Value,
            taxpayerId,fiscalMemoryId=memoryId,fiscalTaxId=reservation,
            invoiceId,invoiceRevision=invoice.Revision,
            requestJson=packet.RequestJson,
            transferSignature=packet.TransferSignature,
            requestTraceId=packet.RequestTraceId,
            timestamp=packet.Timestamp,requestSha256=packet.RequestSha256,
            endpointCode="tax-authority-submit",automaticRetry=false,
            accessTokenStored=false
        });

        var conn=db.Database.GetDbConnection();
        var opened=conn.State!=ConnectionState.Open;
        if(opened)await conn.OpenAsync(ct);
        try
        {
            await using var tx=await conn.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            try
            {
                if((string?)await Scalar(conn,tx,"SELECT current_database()",ct)!="argintaxpayer_test")
                    return Denied("sandbox_database_required");
                var current=await Scalar(conn,tx,"""
SELECT snapshot_sha256 FROM invoice_documents
WHERE customer_user_id=@owner AND taxpayer_id=@taxpayer
 AND id=@invoice AND revision=@revision AND state=3 FOR UPDATE
""",ct,("owner",owner.Value),("taxpayer",taxpayerId),
                    ("invoice",invoiceId),("revision",invoice.Revision));
                if(current is not string hash || hash!=invoice.SnapshotSha256)
                    return Denied("concurrent_invoice_change");
                if((bool?)await Scalar(conn,tx,"""
SELECT EXISTS(SELECT 1 FROM customer_taxpayer_memberships
 WHERE customer_user_id=@owner AND taxpayer_id=@taxpayer
 AND unlinked_at_utc IS NULL)
""",ct,("owner",owner.Value),("taxpayer",taxpayerId))!=true)
                    return Denied("membership_inactive");
                if((bool?)await Scalar(conn,tx,"""
SELECT EXISTS(SELECT 1 FROM invoice_reviews
 WHERE invoice_id=@invoice AND customer_user_id=@owner
 AND taxpayer_id=@taxpayer AND state=4)
""",ct,("owner",owner.Value),("taxpayer",taxpayerId),
                    ("invoice",invoiceId))!=true)
                    return Denied("review_not_approved");

                var previous=await FindExistingInTransaction(conn,tx,
                    owner.Value,taxpayerId,invoiceId,invoice.Revision,ct);
                if(previous is { } old)
                    return old.Prepared
                        ?new(InvoiceApiDecision.Success,"already_prepared",old.Id)
                        :Denied("dispatch_operation_requires_reconciliation");

                var now=DateTime.UtcNow;
                var keyId=$"phase10:legacy:{invoiceId:N}:{invoice.Revision}";
                await Execute(conn,tx,"""
INSERT INTO invoice_dispatch_operations(
 id,customer_user_id,taxpayer_id,fiscal_memory_id,invoice_id,invoice_revision,
 idempotency_key,payload_sha256,state,state_revision,rule_set_edition,
 mapper_version,signing_profile,created_at_utc,updated_at_utc)
VALUES(@id,@owner,@taxpayer,@memory,@invoice,@revision,
 @key,@hash,1,1,@edition,@mapper,@profile,@now,@now)
""",ct,("id",operationId),("owner",owner.Value),("taxpayer",taxpayerId),
                    ("memory",memoryId),("invoice",invoiceId),
                    ("revision",invoice.Revision),("key",keyId),
                    ("hash",packet.RequestSha256),("edition",wire.RuleSetEdition),
                    ("mapper",wire.MapperVersion),
                    ("profile","sandbox-legacy-self-tsp-pkcs8"),("now",now));
                await Execute(conn,tx,"""
INSERT INTO outbox_messages(
 id,taxpayer_id,event_type,payload_json,deduplication_key,
 created_at_utc,processed_at_utc,attempt_count)
VALUES(@id,@taxpayer,@event,@payload,@dedup,@now,NULL,0)
""",ct,("id",operationId),("taxpayer",taxpayerId),
                    ("event","invoice.sandbox.legacy.dispatch.requested"),
                    ("payload",outboxPayload),
                    ("dedup",$"phase10:sandbox:legacy:{taxpayerId:N}:{invoiceId:N}:{invoice.Revision}"),
                    ("now",now));
                await tx.CommitAsync(ct);
                return new(InvoiceApiDecision.Success,"legacy_outbox_prepared",operationId);
            }
            catch(PostgresException ex) when(ex.SqlState is
                PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure)
            {
                await tx.RollbackAsync(CancellationToken.None);
                return Denied("concurrent_preparation_retry_read_status");
            }
            catch(PostgresException ex) when(ex.SqlState==PostgresErrorCodes.ForeignKeyViolation)
            {
                await tx.RollbackAsync(CancellationToken.None);
                return Denied("sandbox_dispatch_fk_migration_required");
            }
        }
        finally { if(opened) await conn.CloseAsync(); }
    }

    private static Phase10LegacyPreparationResult Denied(string code)=>
        new(InvoiceApiDecision.Invalid,code);

    private async Task<string?> ReadReservationAsync(Guid owner,Guid taxpayer,
        Guid invoice,long revision,Guid memory,string hash,CancellationToken ct)
    {
        var c=db.Database.GetDbConnection();
        var opened=c.State!=ConnectionState.Open;
        if(opened)await c.OpenAsync(ct);
        try{return await Scalar(c,null,"""
SELECT fiscal_tax_id FROM sandbox_invoice_fiscal_reservations
WHERE customer_user_id=@owner AND taxpayer_id=@taxpayer AND
 invoice_id=@invoice AND invoice_revision=@revision AND sandbox_memory_id=@memory
 AND approved_snapshot_sha256=@hash
""",ct,("owner",owner),("taxpayer",taxpayer),("invoice",invoice),
            ("revision",revision),("memory",memory),("hash",hash)) as string;}
        finally{if(opened)await c.CloseAsync();}
    }

    private async Task<string?> ReadMemoryCodeAsync(Guid owner,Guid taxpayer,
        Guid memory,CancellationToken ct)
    {
        var c=db.Database.GetDbConnection();
        var opened=c.State!=ConnectionState.Open;
        if(opened)await c.OpenAsync(ct);
        try{return await Scalar(c,null,"""
SELECT memory_identifier FROM sandbox_fiscal_memories
WHERE id=@memory AND customer_user_id=@owner AND taxpayer_id=@taxpayer
 AND private_envelope_json IS NOT NULL
""",ct,("owner",owner),("taxpayer",taxpayer),("memory",memory)) as string;}
        finally{if(opened)await c.CloseAsync();}
    }

    private async Task<string?> ReadInvoiceNumberAsync(Guid invoice,CancellationToken ct)
    {
        var c=db.Database.GetDbConnection();
        var opened=c.State!=ConnectionState.Open;
        if(opened)await c.OpenAsync(ct);
        try{return await Scalar(c,null,"""
SELECT system_code FROM invoice_support_references
WHERE source_id=@invoice AND source_kind IN ('document','workspace')
ORDER BY (source_kind='document') DESC LIMIT 1
""",ct,("invoice",invoice)) as string;}
        finally{if(opened)await c.CloseAsync();}
    }

    private async Task<(Guid Id,bool Prepared)?> FindExistingAsync(
        Guid owner,Guid taxpayer,Guid invoice,long revision,CancellationToken ct)
    {
        var c=db.Database.GetDbConnection();
        var opened=c.State!=ConnectionState.Open;
        if(opened)await c.OpenAsync(ct);
        try{return await FindExistingInTransaction(c,null,owner,taxpayer,invoice,revision,ct);}
        finally{if(opened)await c.CloseAsync();}
    }

    private static async Task<(Guid Id,bool Prepared)?> FindExistingInTransaction(
        DbConnection conn,DbTransaction? tx,Guid owner,Guid taxpayer,
        Guid invoice,long revision,CancellationToken ct)
    {
        await using var cmd=Command(conn,tx,"""
SELECT d.id,EXISTS(SELECT 1 FROM outbox_messages o
 WHERE o.id=d.id AND o.taxpayer_id=d.taxpayer_id
 AND o.event_type='invoice.sandbox.legacy.dispatch.requested')
FROM invoice_dispatch_operations d
WHERE d.customer_user_id=@owner AND d.taxpayer_id=@taxpayer
 AND d.invoice_id=@invoice AND d.invoice_revision=@revision LIMIT 1
""",("owner",owner),("taxpayer",taxpayer),("invoice",invoice),("revision",revision));
        await using var reader=await cmd.ExecuteReaderAsync(ct);
        if(await reader.ReadAsync(ct))return (reader.GetGuid(0),reader.GetBoolean(1));
        return null;
    }

    private static DbCommand Command(DbConnection c,DbTransaction? tx,
        string sql,params (string Name,object Value)[] parameters)
    {
        var cmd=c.CreateCommand();
        cmd.Transaction=tx;
        cmd.CommandText=sql;
        foreach(var (name,value) in parameters)
        {
            var p=cmd.CreateParameter();p.ParameterName=name;p.Value=value;cmd.Parameters.Add(p);
        }
        return cmd;
    }
    private static async Task<object?> Scalar(DbConnection c,DbTransaction? tx,
        string sql,CancellationToken ct,params (string Name,object Value)[] args)
    {
        await using var cmd=Command(c,tx,sql,args);
        return await cmd.ExecuteScalarAsync(ct);
    }
    private static async Task Execute(DbConnection c,DbTransaction tx,
        string sql,CancellationToken ct,params (string Name,object Value)[] args)
    {
        await using var cmd=Command(c,tx,sql,args);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
