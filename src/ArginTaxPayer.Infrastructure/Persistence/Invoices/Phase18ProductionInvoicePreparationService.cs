using System.Data;
using System.Data.Common;
using System.Text.Json;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Parties;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using ArginTaxPayer.Infrastructure.Signing.Connection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>
/// Prepares one owned finalized classified-memory Type1 or original Type2/Pattern1 invoice.
/// The frozen revision, exact active memory, serial, signed encrypted outbox and
/// RESERVED operation are checked/committed atomically. Legal successors require
/// a same-memory predecessor relation backed by a persisted authority SUCCESS report.
/// Credential reads precede the explicitly invoked scoped worker's single POST.
/// </summary>
public sealed class Phase18ProductionInvoicePreparationService(
    ArginTaxPayerDbContext db, TaxpayerScopeService scope, IServiceProvider services)
{
    public async Task<Phase10LegacyPreparationResult> PrepareAsync(string? bearer,
        Guid taxpayerId,Guid invoiceId,CancellationToken ct=default)
    {
        if(taxpayerId==Guid.Empty || invoiceId==Guid.Empty)return Denied("invalid_invoice");
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async()=>
        {
            await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            try
            {
                var result=await PrepareInTransactionAsync(bearer,taxpayerId,invoiceId,ct);
                if(result.Decision==InvoiceApiDecision.Success)await transaction.CommitAsync(ct);
                return result;
            }
            catch(PostgresException ex) when(ex.SqlState is PostgresErrorCodes.UniqueViolation or
                PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return Denied("concurrent_preparation_retry_read_status");
            }
        });
    }

    private async Task<Phase10LegacyPreparationResult> PrepareInTransactionAsync(
        string? bearer,Guid taxpayerId,Guid invoiceId,CancellationToken ct)
    {
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

        var connection=db.Database.GetDbConnection();
        var transaction=db.Database.CurrentTransaction!.GetDbTransaction();
        await Scalar(connection,transaction,"""
SELECT id FROM invoice_documents WHERE id=@invoice AND customer_user_id=@owner AND taxpayer_id=@taxpayer FOR UPDATE
""",ct,("invoice",invoiceId),("owner",owner.Value),("taxpayer",taxpayerId));
        var invoice=await db.InvoiceDocuments.AsNoTracking()
            .SingleOrDefaultAsync(i=>i.Id==invoiceId && i.CustomerUserId==owner.Value &&
                i.TaxpayerId==taxpayerId,ct);
        if(invoice is null)
            return new(InvoiceApiDecision.NotFound,"invoice_not_found");
        if(invoice.Subject is not (InvoiceSubject.Original or InvoiceSubject.Correction or
            InvoiceSubject.Cancellation or InvoiceSubject.SalesReturn))
            return Denied("production_invoice_subject_unsupported");
        if(invoice.State!=InvoiceLifecycleState.Finalized)
            return Denied("invoice_not_finalized");
        if(invoice.IssueDateUtc > DateTime.UtcNow)
            return Denied("invoice_issue_date_future");
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
        try { using var document=JsonDocument.Parse(invoice.SnapshotJson);
            root=document.RootElement.Clone(); }
        catch(JsonException) { return Denied("production_snapshot_incomplete"); }
        if (InvoiceSnapshotClassification.MissingContractNumber(root))
            return Denied("invoice_contract_number_missing");
        var type2 = InvoiceSnapshotClassification.IsType2GeneralSale(root);
        Guid buyerId = Guid.Empty;
        var hasSandboxMemory = root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("sandboxFiscalMemoryId", out var sandboxMemoryField) &&
            sandboxMemoryField.ValueKind != JsonValueKind.Null;
        if(root.ValueKind!=JsonValueKind.Object || !root.TryGetProperty("environment",out var env)||
           env.ValueKind!=JsonValueKind.String || env.GetString() is not ("Production" or "Sandbox") ||
           hasSandboxMemory ||
           !root.TryGetProperty("fiscalMemoryId",out var memoryField) || memoryField.ValueKind!=JsonValueKind.String ||
           !memoryField.TryGetGuid(out var memoryId) || memoryId==Guid.Empty ||
           (!type2 && (!root.TryGetProperty("buyerPartyId",out var buyerField) || buyerField.ValueKind != JsonValueKind.String ||
           !buyerField.TryGetGuid(out buyerId) || buyerId==Guid.Empty)) ||
           !root.TryGetProperty("lines",out var linesField))
            return Denied("production_snapshot_incomplete");

        // Resolve the actual environment from the exact persisted owned memory;
        // the immutable JSON label must agree and never selects a different host.
        string? memoryCode=null;
        int environmentValue=0;
        await using(var memoryCommand=Command(connection,transaction,"""
SELECT m.memory_identifier,m.authority_environment FROM fiscal_memories m
JOIN taxpayer_profiles t ON t.id=m.taxpayer_id
WHERE m.id=@memory AND m.taxpayer_id=@taxpayer AND t.customer_user_id=@owner
 AND m.authority_environment IN (1,2) AND m.status=2
FOR UPDATE OF m
""",("memory",memoryId),("owner",owner.Value),("taxpayer",taxpayerId)))
        await using(var reader=await memoryCommand.ExecuteReaderAsync(ct))
            if(await reader.ReadAsync(ct))
            { memoryCode=reader.GetString(0);environmentValue=reader.GetInt32(1); }
        if(memoryCode is not {Length:6} || memoryCode.Any(c=>!(c is >= 'A' and <= 'Z' or >= '0' and <= '9')))
            return Denied("production_memory_not_ready");
        var environment=Phase18PersistedAuthorityOperationGate.FromPersistedValue(environmentValue);
        var environmentName=environment==TaxAuthorityEnvironment.Production?"Production":"Sandbox";
        if(env.GetString()!=environmentName)return Denied("invoice_memory_environment_mismatch");
        if(invoice.Subject!=InvoiceSubject.Original)
        {
            if(environment!=TaxAuthorityEnvironment.Production)
                return Denied("production_legal_relation_environment_required");
            var predecessorReference = root.TryGetProperty("referenceTaxId", out var referenceField) &&
                referenceField.ValueKind == JsonValueKind.String ? referenceField.GetString() : null;
            if(predecessorReference is not { Length: 22 })
                return Denied("production_legal_relation_not_verified");
            var relationVerified=await Scalar(connection,transaction,"""
SELECT EXISTS (
 SELECT 1 FROM authority_invoice_legal_relations lr
 JOIN authority_invoice_fiscal_reservations pr ON pr.customer_user_id=lr.customer_user_id
   AND pr.taxpayer_id=lr.taxpayer_id AND pr.invoice_id=lr.predecessor_invoice_id
   AND pr.fiscal_memory_id=@memory AND pr.authority_environment=1
 JOIN production_authority_inquiry_reports ir ON ir.operation_id=lr.predecessor_operation_id
   AND ir.customer_user_id=lr.customer_user_id AND ir.taxpayer_id=lr.taxpayer_id
   AND ir.invoice_id=lr.predecessor_invoice_id
 WHERE lr.customer_user_id=@owner AND lr.taxpayer_id=@taxpayer
   AND lr.successor_invoice_id=@invoice AND lr.subject=@subject
   AND lr.state IN (1,2,4) AND lr.predecessor_fiscal_tax_id=@reference
   AND @environment=1
   AND ir.report_json->>'Status'='SUCCESS'
)
""",ct, ("memory",memoryId),("environment",environmentValue),
                ("owner",owner.Value),("taxpayer",taxpayerId),("invoice",invoiceId),
                ("subject",(int)invoice.Subject),("reference",predecessorReference));
            if(relationVerified is not true)return Denied("production_legal_relation_not_verified");
        }
        var binding=new AuthorityOperationBinding(owner.Value,taxpayerId,invoiceId,memoryId,
            environment);
        var existing=await FindExistingAsync(connection,transaction,owner.Value,taxpayerId,invoiceId,invoice.Revision,memoryId,environmentValue,ct);
        if(existing is { } previous)
            return previous.Prepared
                ?new(InvoiceApiDecision.Success,"already_prepared",previous.Id)
                :Denied("dispatch_operation_requires_reconciliation");

        InvoiceEntryLine[] lines;
        try { lines=JsonSerializer.Deserialize<InvoiceEntryLine[]>(linesField.GetRawText())??[]; }
        catch(JsonException){return Denied("invoice_lines_invalid");}
        if(lines.Length==0 || lines.Any(l=>l.CatalogItemId is null ||
                l.UnitId is null || l.OfficialIdentifierSnapshot is not { Length: 13 } code ||
                !code.All(char.IsAsciiDigit)))
            return Denied("official_invoice_lines_incomplete");

        var sellerRow=await db.InvoiceParticipantDetails.AsNoTracking()
            .Where(t=>t.TaxpayerId==taxpayerId && t.CustomerUserId==owner.Value)
            .Select(t=>new { t.EconomicCode, t.BranchCode }).SingleOrDefaultAsync(ct);
        var buyerRow=await (from link in db.CustomerPartyLinks.AsNoTracking()
            join party in db.Parties.AsNoTracking() on link.PartyId equals party.Id
            where link.CustomerUserId==owner.Value && link.PartyId==buyerId &&
                link.UnlinkedAtUtc==null && party.Status==PartyStatus.Active
            select new { Identifier=party.NormalizedIdentifier, PersonType=(int?)party.Kind })
            .SingleOrDefaultAsync(ct);
        var buyerDetails=await db.InvoiceParticipantDetails.AsNoTracking()
            .Where(t=>t.PartyId==buyerId && t.CustomerUserId==owner.Value)
            .Select(t=>new { t.EconomicCode, t.BranchCode, t.PostalCode })
            .SingleOrDefaultAsync(ct);
        var seller=sellerRow?.EconomicCode;
        var buyer=buyerDetails?.EconomicCode ?? buyerRow?.Identifier;
        if(string.IsNullOrWhiteSpace(seller) || (!type2 && string.IsNullOrWhiteSpace(buyer)))
            return Denied("seller_or_buyer_identity_missing");
        if(seller.Length is not (11 or 14) || !seller.All(char.IsAsciiDigit))
            return Denied("seller_economic_identifier_invalid");

        int? buyerPersonTypeOverride = root.TryGetProperty("buyerPersonType", out _)
            ? null : buyerRow?.PersonType;
        decimal? otherTaxAndLegalFeesOverride = root.TryGetProperty("otherTaxAndLegalFeesRial", out _)
            ? null : 0m;
        var calculationAudit = Phase10InvoiceCalculationAuditService.AuditSnapshot(
            invoice.SnapshotJson, invoice.IssueDateUtc,
            buyerPersonTypeOverride, otherTaxAndLegalFeesOverride);
        if (!calculationAudit.CanResend)
            return Denied("invoice_calculation_audit_failed");
        var buyerPersonType = buyerPersonTypeOverride;
        long? otherTaxAndLegalFees = otherTaxAndLegalFeesOverride is not null ? 0L : null;

        var unitIds=lines.Select(l=>l.UnitId!.Value).Distinct().ToArray();
        var units=await db.OfficialMeasurementUnits.AsNoTracking()
            .Where(u=>unitIds.Contains(u.Id) && u.Status==1 &&
                db.CatalogReferenceReleases.Any(r=>r.Id==u.ReleaseId && r.State==3))
            .Select(u=>new{u.Id,u.Code}).ToDictionaryAsync(u=>u.Id,u=>u.Code,ct);
        if(units.Count!=unitIds.Length)
            return Denied("official_units_unverified");

        var configuration=services.GetRequiredService<IConfiguration>();
        if(!Phase18LegacyAuthorityEndpointSelector.TryResolveOperationEndpoint(binding,binding,
            environment,configuration,"GET_TOKEN",out var tokenEndpoint) ||
           !Phase18LegacyAuthorityEndpointSelector.TryResolveOperationEndpoint(binding,binding,
            environment,configuration,"GET_SERVER_INFORMATION",out var serverEndpoint) ||
           tokenEndpoint is null || serverEndpoint is null)
            return Denied("production_legacy_endpoint_disabled");

        // Scope and immutable revision checks all precede decryption or authority IO.
        var loader=services.GetRequiredService<DatabaseLegacySigningKeyLoader>();
        using var key=await loader.RecoverAsync(owner.Value,taxpayerId,memoryId,ct);
        if(key is null)return Denied("production_legacy_key_unavailable");

        // Include known same-memory external IDs only as upper-bound evidence.
        // Do not borrow a tax ID or serial from another invoice or memory.
        var knownSnapshots=await db.InvoiceDocuments.AsNoTracking()
            .Where(i=>i.CustomerUserId==owner.Value && i.TaxpayerId==taxpayerId)
            .Select(i=>i.SnapshotJson).ToListAsync(ct);
        var knownIds=new List<string>();
        foreach(var snapshot in knownSnapshots)
        {
            try
            {
                using var parsed=JsonDocument.Parse(snapshot);
                var candidate=parsed.RootElement;
                if(candidate.ValueKind==JsonValueKind.Object && candidate.TryGetProperty("fiscalMemoryId",out var m) &&
                    m.ValueKind==JsonValueKind.String && m.TryGetGuid(out var id) && id==memoryId)
                    foreach(var field in new[]{"fiscalTaxId","taxId","referenceTaxId"})
                        if(candidate.TryGetProperty(field,out var value) && value.ValueKind==JsonValueKind.String && value.GetString() is { } taxId)
                            knownIds.Add(taxId);
            }
            catch(JsonException) { }
        }
        ProductionFiscalReservation reservation;
        try
        {
            reservation=await ProductionInvoiceFiscalSerialRepository.ReserveAsync(connection,transaction,
                invoice,memoryId,memoryCode,environmentValue,configuration[$"{environmentName}InvoiceLegacy:LastUsedSerial:{memoryId:N}"],knownIds,ct);
        }
        catch(ArgumentException) { return Denied("production_fiscal_serial_invalid_or_exhausted"); }
        catch(InvalidOperationException) { return Denied("production_fiscal_reservation_conflict"); }

        InvoiceWireProjection wire;
        try
        {
            wire=Phase10LegacyFrozenInvoiceProjector.Project(invoice,reservation.FiscalTaxId,
                seller,buyer,units,null,buyerPersonType,otherTaxAndLegalFees,
                buyerRow?.Identifier,buyerDetails?.BranchCode,buyerDetails?.PostalCode,
                sellerRow?.BranchCode,validatedAuthorityBinding:binding);
        }
        catch(Exception exception) when(exception is ArgumentException or InvalidOperationException or
            NotSupportedException or OverflowException or KeyNotFoundException or JsonException)
        { return Denied("legacy_wire_mapping_unverified"); }

        var tokens=services.GetRequiredService<LegacyInvoiceAccessTokenClient>();
        var encryption=services.GetRequiredService<LegacySandboxServerEncryptionKeyClient>();
        using var credentials=await LegacyInvoicePreparationCredentials.RequestVerifiedAsync(
            tokens,encryption,tokenEndpoint,serverEndpoint,memoryCode,key.PrivateKey,key.PublicSpkiDer,ct);
        if(!credentials.Token.Authenticated || credentials.Token.AccessToken is not { } token)
            return Denied(credentials.Token.SafeCode);
        if(credentials.EncryptionKey is not { } authorityKey)
            return Denied("production_legacy_authority_encryption_key_unavailable");
        var operationId=Guid.NewGuid();
        var packet=LegacyInvoiceSubmissionPacketBuilder.BuildEncrypted(operationId,owner.Value,taxpayerId,
            reservation.FiscalTaxId,wire.Json,key.PrivateKey,key.PublicSpkiDer,token,Guid.NewGuid(),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
            memoryCode,authorityKey);
        var payload=JsonSerializer.Serialize(new {
            schema=environment==TaxAuthorityEnvironment.Production?"phase18.production.legacy.invoice.dispatch.v1":"phase10.sandbox.legacy.invoice.dispatch.v1",environment=environmentName,
            operationId,customerUserId=owner.Value,taxpayerId,fiscalMemoryId=memoryId,
            fiscalTaxId=reservation.FiscalTaxId,invoiceId,invoiceRevision=invoice.Revision,
            requestJson=packet.RequestJson,transferSignature=packet.TransferSignature,
            requestTraceId=packet.RequestTraceId,timestamp=packet.Timestamp,requestSha256=packet.RequestSha256,
            endpointCode="tax-authority-submit",automaticRetry=false,accessTokenStored=false
        });
        var now=DateTime.UtcNow;
        await Execute(connection,transaction,"""
INSERT INTO invoice_dispatch_operations
(id,customer_user_id,taxpayer_id,fiscal_memory_id,invoice_id,invoice_revision,idempotency_key,
 payload_sha256,state,state_revision,rule_set_edition,mapper_version,signing_profile,
 created_at_utc,updated_at_utc,authority_environment)
VALUES(@id,@owner,@taxpayer,@memory,@invoice,@revision,@key,@hash,3,1,@edition,@mapper,@profile,@now,@now,@environment)
""",ct,("id",operationId),("owner",owner.Value),("taxpayer",taxpayerId),("memory",memoryId),
            ("invoice",invoiceId),("revision",invoice.Revision),("key",$"phase18:{environmentName.ToLowerInvariant()}:legacy:{invoiceId:N}:{invoice.Revision}"),
            ("hash",packet.RequestSha256),("edition",wire.RuleSetEdition),("mapper",wire.MapperVersion),
            ("profile",$"{environmentName.ToLowerInvariant()}-legacy-self-tsp-pkcs8"),("now",now),("environment",environmentValue));
        await Execute(connection,transaction,"""
INSERT INTO outbox_messages
(id,taxpayer_id,event_type,payload_json,deduplication_key,created_at_utc,processed_at_utc,attempt_count)
VALUES(@id,@taxpayer,@event,@payload,@dedup,@now,NULL,0)
""",ct,("id",operationId),("taxpayer",taxpayerId),("payload",payload),("event",$"invoice.{environmentName.ToLowerInvariant()}.legacy.dispatch.requested"),
            ("dedup",$"phase18:{environmentName.ToLowerInvariant()}:legacy:{taxpayerId:N}:{invoiceId:N}:{invoice.Revision}"),("now",now));
        return new(InvoiceApiDecision.Success,"legacy_outbox_prepared",operationId,wire.Json);
    }

    private static Phase10LegacyPreparationResult Denied(string code)=>new(InvoiceApiDecision.Invalid,code);

    private static async Task<(Guid Id,bool Prepared)?> FindExistingAsync(DbConnection c,DbTransaction tx,
        Guid owner,Guid taxpayer,Guid invoice,long revision,Guid memory,int environment,CancellationToken ct)
    {
        await using var command=Command(c,tx,"""
SELECT d.id, d.authority_environment=@environment AND d.fiscal_memory_id=@memory AND d.state=3
 AND EXISTS(SELECT 1 FROM outbox_messages o WHERE o.id=d.id AND o.taxpayer_id=d.taxpayer_id
   AND o.event_type=@event AND o.processed_at_utc IS NULL AND o.attempt_count=0)
 AND NOT EXISTS(SELECT 1 FROM invoice_dispatch_attempts a WHERE a.operation_id=d.id)
FROM invoice_dispatch_operations d
WHERE d.customer_user_id=@owner AND d.taxpayer_id=@taxpayer AND d.invoice_id=@invoice
 AND d.invoice_revision=@revision LIMIT 1
""",("owner",owner),("taxpayer",taxpayer),("invoice",invoice),("revision",revision),("memory",memory),("environment",environment),
            ("event",environment==1?"invoice.production.legacy.dispatch.requested":"invoice.sandbox.legacy.dispatch.requested"));
        await using var reader=await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)?(reader.GetGuid(0),!reader.IsDBNull(1) && reader.GetBoolean(1)):null;
    }

    internal static DbCommand Command(DbConnection c,DbTransaction? tx,string sql,
        params (string Name,object Value)[] parameters)
    {
        var command=c.CreateCommand();command.Transaction=tx;command.CommandText=sql;
        foreach(var(name,value) in parameters)
        {var p=command.CreateParameter();p.ParameterName=name;p.Value=value;command.Parameters.Add(p);}
        return command;
    }
    internal static async Task<object?> Scalar(DbConnection c,DbTransaction? tx,string sql,
        CancellationToken ct,params (string Name,object Value)[] parameters)
    {await using var command=Command(c,tx,sql,parameters);return await command.ExecuteScalarAsync(ct);}
    private static async Task Execute(DbConnection c,DbTransaction tx,string sql,
        CancellationToken ct,params (string Name,object Value)[] parameters)
    {await using var command=Command(c,tx,sql,parameters);await command.ExecuteNonQueryAsync(ct);}
}
