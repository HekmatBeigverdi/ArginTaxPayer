using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

internal sealed record ProductionCorrectionSuccessorWrite(
    Guid WorkspaceId,
    long ExpectedRevision,
    Guid PredecessorInvoiceId,
    Guid PredecessorOperationId,
    StoredInvoice Invoice,
    StoredInvoiceRevision Revision,
    StoredInvoiceReview Review,
    string ReferenceTaxId);

/// <summary>Independent V7.9 correction draft, immutable predecessor, one send operation.</summary>
public sealed class Phase18ProductionCorrectionService(
    ArginTaxPayerDbContext db,
    TaxpayerScopeService scope,
    Phase18InvoiceInquiryService inquiry,
    Phase10InvoiceDispatchReadService dispatch,
    InvoiceSubscriptionGate subscriptions,
    Phase18InvoiceSubmissionService sender,
    IConfiguration config)
{
    public async Task<CorrectionOperationResult> OpenAsync(
        string? bearer,
        Guid taxpayerId,
        Guid predecessorId,
        CancellationToken ct)
    {
        var owner = await ResolveOwnerAsync(bearer, taxpayerId, ct);
        if (owner is null)
        {
            return new(InvoiceApiDecision.Forbidden, "not_authorized");
        }

        var current = await FindActiveDraftAsync(owner.Value, taxpayerId, predecessorId, ct);
        if (current is { } existingDraft)
        {
            var existingWithBaseline = await InvoiceLegalQuantityAdjustments.CorrectionDraftWithBaselineAsync(
                db, owner.Value, taxpayerId, predecessorId, existingDraft.Json, production: true, ct);
            return new(
                InvoiceApiDecision.Success,
                "existing_draft",
                CreateDraftView(existingDraft.Id, predecessorId, existingDraft.Revision, existingWithBaseline));
        }

        var original = await db.InvoiceDocuments.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == predecessorId && x.TaxpayerId == taxpayerId && x.CustomerUserId == owner.Value, ct);
        if (original is null)
        {
            return new(InvoiceApiDecision.NotFound, "not_found");
        }
        if (original.State != InvoiceLifecycleState.Finalized || original.Subject == InvoiceSubject.Cancellation)
        {
            return new(InvoiceApiDecision.Conflict, "invalid_predecessor");
        }

        var sourceDispatch = await dispatch.GetAsync(bearer, taxpayerId, predecessorId, ct);
        if (sourceDispatch.Decision != InvoiceApiDecision.Success ||
            sourceDispatch.Overview is not { Status: "Acknowledged", Environment: "Production",
                ReferenceNumber: not null } sourceOperation)
            return new(InvoiceApiDecision.Conflict, "accepted_predecessor_required");
        var official = await inquiry.ReadAsync(bearer, taxpayerId, predecessorId, ct);
        if (official.Decision != InvoiceApiDecision.Success || official.AuthorityResult?.Status != "SUCCESS" ||
            official.ReferenceNumber != sourceOperation.ReferenceNumber)
        {
            return new(InvoiceApiDecision.Conflict, "accepted_predecessor_required");
        }

        var referenceTaxId = await db.Database.SqlQuery<string>($"""
            SELECT fiscal_tax_id AS "Value" FROM authority_invoice_fiscal_reservations
            WHERE customer_user_id={owner.Value} AND taxpayer_id={taxpayerId}
             AND invoice_id={predecessorId} LIMIT 1
            """).FirstOrDefaultAsync(ct);
        if (referenceTaxId is not { Length: 22 })
        {
            return new(InvoiceApiDecision.Conflict, "reference_taxid_missing");
        }
        if (await db.Database.SqlQuery<int>($"""
            SELECT 1 AS "Value" FROM authority_invoice_legal_relations
            WHERE customer_user_id={owner.Value} AND taxpayer_id={taxpayerId}
             AND predecessor_invoice_id={predecessorId} AND subject=2
             AND state IN (1,2,4) LIMIT 1
            """).AnyAsync(ct))
        {
            return new(InvoiceApiDecision.Conflict, "active_related_invoice");
        }

        var snapshot = JsonNode.Parse(original.SnapshotJson)?.AsObject();
        if (snapshot is null || (string?)snapshot["environment"] != "Production" ||
            (int?)snapshot["invoiceType"] != 1 || (int?)snapshot["pattern"] != 1)
        {
            return new(InvoiceApiDecision.Invalid, "unsupported_pattern");
        }

        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        snapshot["referenceTaxId"] = referenceTaxId;
        snapshot["predecessorInvoiceId"] = predecessorId.ToString("D");
        snapshot["predecessorOperationId"] = sourceOperation.OperationId.ToString("D");
        snapshot["legalSubject"] = 2;
        var sourceLines = JsonSerializer.Deserialize<InvoiceEntryLine[]>(snapshot["lines"]!.ToJsonString()) ?? [];
        var correctionBasis = await InvoiceLegalQuantityAdjustments.ReadSalesReturnBasisAsync(
            db, owner.Value, taxpayerId, predecessorId, production: true, ct);
        var correctedBaseline = correctionBasis.AvailableLines(reserveOne: false);
        snapshot["lines"] = JsonSerializer.SerializeToNode(correctedBaseline);
        snapshot["correctionBaselineQuantities"] = JsonSerializer.SerializeToNode(correctedBaseline.Select(x => x.Quantity));
        var json = snapshot.ToJsonString();
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO authority_invoice_correction_workspaces
                (id,customer_user_id,taxpayer_id,predecessor_invoice_id,predecessor_fiscal_tax_id,
                 revision,draft_json,state,created_at_utc,updated_at_utc)
                VALUES({id},{owner.Value},{taxpayerId},{predecessorId},{referenceTaxId},
                 1,CAST({json} AS jsonb),1,{now},{now})
                """, ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            current = await FindActiveDraftAsync(owner.Value, taxpayerId, predecessorId, ct);
            if (current is { } concurrentDraft)
            {
                return new(
                    InvoiceApiDecision.Success,
                    "existing_draft",
                    CreateDraftView(concurrentDraft.Id, predecessorId, concurrentDraft.Revision, concurrentDraft.Json));
            }

            return new(InvoiceApiDecision.Conflict, "draft_conflict");
        }

        return new(InvoiceApiDecision.Success, "draft_created", CreateDraftView(id, predecessorId, 1, json));
    }

    public async Task<CorrectionOperationResult> ReadAsync(
        string? bearer,
        Guid taxpayerId,
        Guid predecessorId,
        CancellationToken ct)
    {
        var owner = await ResolveOwnerAsync(bearer, taxpayerId, ct);
        if (owner is null)
        {
            return new(InvoiceApiDecision.Forbidden, "not_authorized");
        }

        var draft = await FindActiveDraftAsync(owner.Value, taxpayerId, predecessorId, ct);
        if (draft is not { } savedDraft)
        {
            return new(InvoiceApiDecision.NotFound, "draft_not_found");
        }

        var savedWithBaseline = await InvoiceLegalQuantityAdjustments.CorrectionDraftWithBaselineAsync(
            db, owner.Value, taxpayerId, predecessorId, savedDraft.Json, production: true, ct);
        return new(
            InvoiceApiDecision.Success,
            "draft_loaded",
            CreateDraftView(savedDraft.Id, predecessorId, savedDraft.Revision, savedWithBaseline));
    }

    public async Task<CorrectionOperationResult> SaveAsync(
        string? bearer,
        Guid taxpayerId,
        Guid predecessorId,
        CorrectionSaveRequest input,
        CancellationToken ct)
    {
        var owner = await ResolveOwnerAsync(bearer, taxpayerId, ct);
        if (owner is null)
        {
            return new(InvoiceApiDecision.Forbidden, "not_authorized");
        }

        var draft = await FindActiveDraftAsync(owner.Value, taxpayerId, predecessorId, ct);
        if (draft is not { } savedDraft)
        {
            return new(InvoiceApiDecision.NotFound, "draft_not_found");
        }
        if (savedDraft.Revision != input.ExpectedRevision)
        {
            return new(InvoiceApiDecision.Conflict, "stale_revision");
        }

        var source = await db.InvoiceDocuments.AsNoTracking().FirstAsync(x =>
            x.Id == predecessorId && x.TaxpayerId == taxpayerId && x.CustomerUserId == owner.Value, ct);
        using var originalDoc = JsonDocument.Parse(source.SnapshotJson);
        var baseline = JsonSerializer.Deserialize<InvoiceEntryLine[]>(
            originalDoc.RootElement.GetProperty("lines").GetRawText()) ?? [];
        var correctionBasis = await InvoiceLegalQuantityAdjustments.ReadSalesReturnBasisAsync(
            db, owner.Value, taxpayerId, predecessorId, production: true, ct);
        baseline = correctionBasis.AvailableLines(reserveOne: false);
        if (input.ReductionEvidenceReference is { Length: > 256 })
        {
            return new(InvoiceApiDecision.Invalid, "original_mistake_evidence_required");
        }

        var edited = baseline.Select((line, index) => index < input.Lines.Length
            ? line with
            {
                Quantity = input.Lines[index].Quantity,
                UnitPriceRial = input.Lines[index].UnitPriceRial,
                DiscountPerUnitRial = input.Lines[index].DiscountPerUnitRial
            }
            : line).ToArray();
        var quantityCorrection = Phase10CorrectionValidation.IsQuantityCorrection(
            baseline, edited, input.ReductionCorrectsOriginalMistake);
        if (input.Lines.Length != baseline.Length ||
            !Phase10CorrectionValidation.AllowedLines(baseline, edited, quantityCorrection))
        {
            return new(InvoiceApiDecision.Invalid, "illegal_correction_fields");
        }
        if (edited.Any(x => x.Quantity * x.UnitPriceRial != decimal.Truncate(x.Quantity * x.UnitPriceRial) ||
            x.Quantity * x.DiscountPerUnitRial != decimal.Truncate(x.Quantity * x.DiscountPerUnitRial)))
        {
            return new(InvoiceApiDecision.Invalid, "non_integral_rial");
        }

        InvoiceFinancialPreview financial;
        try
        {
            financial = InvoiceFinancialCalculator.Preview(edited.Select(x => new InvoiceFinancialInput(
                x.CatalogItemId ?? Guid.Empty, x.Quantity, x.UnitPriceRial,
                decimal.Truncate(x.Quantity * x.DiscountPerUnitRial), x.VatRatePercent)).ToArray(),
                new InvoiceTypePattern(ElectronicInvoiceType.First, 1),
                new InvoiceRoundingPolicy(OfficialV79InvoicePatternMatrix.Edition));
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException)
        {
            return new(InvoiceApiDecision.Invalid, "invalid_amounts");
        }

        // Preserve the original fees while recalculating the editable invoice lines.
        var otherTaxAndLegalFees = originalDoc.RootElement.GetProperty("otherTaxAndLegalFeesRial").GetDecimal();
        var total = (financial.GrandTotalRial ?? 0m) + otherTaxAndLegalFees;
        var cashAmount = input.SettlementMethod switch
        {
            1 => total,
            2 => 0m,
            _ => input.CashAmountRial
        };
        var creditAmount = input.SettlementMethod switch
        {
            2 => total,
            1 => 0m,
            _ => input.CreditAmountRial
        };
        if (cashAmount < 0 || creditAmount < 0 ||
            cashAmount + creditAmount != total ||
            (input.SettlementMethod == 1 && creditAmount != 0) ||
            (input.SettlementMethod == 2 && cashAmount != 0) ||
            (input.SettlementMethod == 3 && (cashAmount <= 0 || creditAmount <= 0)) ||
            input.SettlementMethod is < 1 or > 3)
        {
            return new(InvoiceApiDecision.Invalid, "settlement_invalid");
        }

        var root = JsonNode.Parse(savedDraft.Json)!.AsObject();
        root["lines"] = JsonSerializer.SerializeToNode(edited);
        root["correctionBaselineQuantities"] = JsonSerializer.SerializeToNode(baseline.Select(x => x.Quantity));
        root["settlementMethod"] = input.SettlementMethod;
        root["cashAmountRial"] = cashAmount;
        root["creditAmountRial"] = creditAmount;
        root["financialSummary"] = JsonSerializer.SerializeToNode(new InvoiceDraftFinancialSummary(
            financial.BeforeDiscountRial, financial.DiscountRial, financial.AfterDiscountRial,
            financial.VatRial ?? 0m, financial.GrandTotalRial ?? 0m));
        root["reductionCorrectsOriginalMistake"] = quantityCorrection;
        root["reductionEvidenceReference"] = quantityCorrection
            ? string.IsNullOrWhiteSpace(input.ReductionEvidenceReference)
                ? "correction-workspace:" + savedDraft.Id.ToString("D") : input.ReductionEvidenceReference
            : null;
        var json = root.ToJsonString();
        var now = DateTime.UtcNow;
        var rows = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE authority_invoice_correction_workspaces
            SET draft_json=CAST({json} AS jsonb),revision=revision+1,updated_at_utc={now}
            WHERE id={savedDraft.Id} AND customer_user_id={owner.Value} AND taxpayer_id={taxpayerId}
             AND predecessor_invoice_id={predecessorId} AND revision={savedDraft.Revision} AND state=1
            """, ct);
        if (rows != 1)
        {
            return new(InvoiceApiDecision.Conflict, "concurrent_change");
        }

        return new(
            InvoiceApiDecision.Success,
            "draft_saved",
            CreateDraftView(savedDraft.Id, predecessorId, savedDraft.Revision + 1, json));
    }

    public async Task<CorrectionOperationResult> DiscardAsync(
        string? bearer,
        Guid taxpayerId,
        Guid predecessorId,
        CancellationToken ct)
    {
        var owner = await ResolveOwnerAsync(bearer, taxpayerId, ct);
        if (owner is null)
        {
            return new(InvoiceApiDecision.Forbidden, "not_authorized");
        }

        var rows = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE authority_invoice_correction_workspaces SET state=3,updated_at_utc={DateTime.UtcNow}
            WHERE customer_user_id={owner.Value} AND taxpayer_id={taxpayerId}
             AND predecessor_invoice_id={predecessorId} AND state=1
            """, ct);
        return new(InvoiceApiDecision.Success, rows > 0 ? "draft_discarded" : "nothing_to_discard");
    }

    public async Task<CorrectionOperationResult> SubmitAsync(
        string? bearer,
        Guid taxpayerId,
        Guid predecessorId,
        CancellationToken ct)
    {
        var actor = await scope.ResolveAsync(bearer, ct);
        var owner = await ResolveOwnerAsync(bearer, taxpayerId, ct);
        if (owner is null)
        {
            return new(InvoiceApiDecision.Forbidden, "not_authorized");
        }

        var subscriptionError = await subscriptions.CheckAsync(owner.Value, ct);
        if (subscriptionError is not null)
            return new(InvoiceApiDecision.Conflict, subscriptionError);

        var draft = await FindActiveDraftAsync(owner.Value, taxpayerId, predecessorId, ct);
        if (draft is not { } savedDraft)
        {
            return await ResumeUnpreparedSuccessorAsync(
                bearer, owner.Value, taxpayerId, predecessorId, ct);
        }

        var sourceDispatch = await dispatch.GetAsync(bearer, taxpayerId, predecessorId, ct);
        if (sourceDispatch.Decision != InvoiceApiDecision.Success ||
            sourceDispatch.Overview is not { Status: "Acknowledged", Environment: "Production",
                ReferenceNumber: not null } sourceOperation)
            return new(InvoiceApiDecision.Conflict, "predecessor_not_accepted");
        var accepted = await inquiry.ReadAsync(bearer, taxpayerId, predecessorId, ct);
        if (accepted.Decision != InvoiceApiDecision.Success || accepted.AuthorityResult?.Status != "SUCCESS" ||
            accepted.ReferenceNumber != sourceOperation.ReferenceNumber)
        {
            return new(InvoiceApiDecision.Conflict, "predecessor_not_accepted");
        }

        var original = await db.InvoiceDocuments.AsNoTracking().FirstAsync(x =>
            x.Id == predecessorId && x.TaxpayerId == taxpayerId && x.CustomerUserId == owner.Value, ct);
        var root = JsonNode.Parse(savedDraft.Json)!.AsObject();
        using var origDoc = JsonDocument.Parse(original.SnapshotJson);
        var originalLines = JsonSerializer.Deserialize<InvoiceEntryLine[]>(
            origDoc.RootElement.GetProperty("lines").GetRawText()) ?? [];
        var issuedOriginalLines = originalLines;
        var correctionBasis = await InvoiceLegalQuantityAdjustments.ReadSalesReturnBasisAsync(
            db, owner.Value, taxpayerId, predecessorId, production: true, ct);
        originalLines = correctionBasis.AvailableLines(reserveOne: false);
        var changedLines = JsonSerializer.Deserialize<InvoiceEntryLine[]>(
            root["lines"]!.ToJsonString()) ?? [];
        var reductionCorrectsOriginalMistake = (bool?)root["reductionCorrectsOriginalMistake"] ?? false;
        if (!Phase10CorrectionValidation.AllowedLines(originalLines, changedLines, reductionCorrectsOriginalMistake))
        {
            return new(InvoiceApiDecision.Invalid, "invalid_correction_lines");
        }

        // A quantity reduced by an already issued sales return is itself a valid
        // correction relative to the immutable invoice, even if it matches the adjusted baseline.
        var linesChanged = issuedOriginalLines.Zip(changedLines).Any(pair =>
            pair.First.Quantity != pair.Second.Quantity ||
            pair.First.UnitPriceRial != pair.Second.UnitPriceRial ||
            pair.First.DiscountPerUnitRial != pair.Second.DiscountPerUnitRial);
        if (!linesChanged &&
            origDoc.RootElement.GetProperty("settlementMethod").GetInt32() == (int?)root["settlementMethod"] &&
            origDoc.RootElement.GetProperty("cashAmountRial").GetDecimal() == (decimal?)root["cashAmountRial"] &&
            origDoc.RootElement.GetProperty("creditAmountRial").GetDecimal() == (decimal?)root["creditAmountRial"])
        {
            return new(InvoiceApiDecision.Invalid, "no_changes_to_correct");
        }

        if (reductionCorrectsOriginalMistake && string.IsNullOrWhiteSpace((string?)root["reductionEvidenceReference"]))
        {
            return new(InvoiceApiDecision.Invalid, "original_mistake_evidence_required");
        }
        if (config["Phase18Production:LegalSuccessorsEnabled"] != "true" ||
            config["Phase18Production:LegalSuccessorsOperatorApproved"] != "true")
        {
            return new(InvoiceApiDecision.Invalid, "production_legal_successors_disabled");
        }

        // Create the successor without modifying the accepted predecessor.
        var now = DateTime.UtcNow;
        var invoiceId = Guid.NewGuid();
        root["sourceWorkspaceId"] = invoiceId.ToString("D");
        root["sourceRevision"] = 1;
        root["issueDateUtc"] = now;
        root["legalSubject"] = 2;
        var json = root.ToJsonString();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
        var successor = new StoredInvoice
        {
            Id = invoiceId,
            CustomerUserId = owner.Value,
            TaxpayerId = taxpayerId,
            Revision = 1,
            State = InvoiceLifecycleState.Finalized,
            Subject = InvoiceSubject.Correction,
            IssueDateUtc = now,
            SnapshotJson = json,
            SnapshotSha256 = hash,
            UpdatedAtUtc = now
        };
        var successorRevision = new StoredInvoiceRevision
        {
            InvoiceId = invoiceId,
            CustomerUserId = owner.Value,
            TaxpayerId = taxpayerId,
            Revision = 1,
            SnapshotJson = json,
            SnapshotSha256 = hash,
            CapturedAtUtc = now
        };
        var successorReview = new StoredInvoiceReview
        {
            InvoiceId = invoiceId,
            CustomerUserId = owner.Value,
            TaxpayerId = taxpayerId,
            Revision = 1,
            State = InvoiceReviewState.Approved,
            UpdatedAtUtc = now,
            LastActorUserId = actor.ActorId
        };
        var write = new ProductionCorrectionSuccessorWrite(
            savedDraft.Id, savedDraft.Revision, predecessorId, sourceOperation.OperationId,
            successor, successorRevision, successorReview, (string?)root["referenceTaxId"] ?? "");
        var persistenceResult = await PersistSuccessorAsync(write, ct);
        if (persistenceResult is not null)
        {
            return persistenceResult;
        }

        // Send only after the successor, revision, review and legal relation commit together.
        var result = await sender.SendAsync(bearer, taxpayerId, invoiceId, ct);
        return new(
            result.Decision,
            result.Code,
            Draft: null,
            InvoiceId: invoiceId,
            Status: result.Status,
            ReferenceNumber: result.ReferenceNumber,
            InquiryCode: result.InquiryCode);
    }

    internal static bool CanResumeUnpreparedSuccessor(bool productionRelation,
        bool activeRelation, bool hasDispatch, bool hasFiscalReservation) =>
        productionRelation && activeRelation && !hasDispatch && !hasFiscalReservation;

    private async Task<CorrectionOperationResult> ResumeUnpreparedSuccessorAsync(
        string? bearer, Guid owner, Guid taxpayerId, Guid predecessorId,
        CancellationToken ct)
    {
        if (config["Phase18Production:LegalSuccessorsEnabled"] != "true" ||
            config["Phase18Production:LegalSuccessorsOperatorApproved"] != "true")
            return new(InvoiceApiDecision.Conflict, "production_legal_successors_disabled");

        var successor = await db.Database.SqlQuery<Guid>($"""
            SELECT successor_invoice_id AS "Value"
            FROM authority_invoice_legal_relations
            WHERE customer_user_id={owner} AND taxpayer_id={taxpayerId}
             AND predecessor_invoice_id={predecessorId}
             AND subject=2 AND state IN (1,2,4)
            """).SingleOrDefaultAsync(ct);
        if (successor == Guid.Empty)
            return new(InvoiceApiDecision.NotFound, "draft_not_found");

        var predecessorDispatch = await dispatch.GetAsync(bearer, taxpayerId, predecessorId, ct);
        if (predecessorDispatch.Decision != InvoiceApiDecision.Success ||
            predecessorDispatch.Overview is not { Status: "Acknowledged", Environment: "Production",
                ReferenceNumber: not null } sourceOperation)
            return new(InvoiceApiDecision.Conflict, "predecessor_not_accepted");
        var accepted = await inquiry.ReadAsync(bearer, taxpayerId, predecessorId, ct);
        if (accepted.Decision != InvoiceApiDecision.Success ||
            accepted.AuthorityResult?.Status != "SUCCESS" ||
            accepted.ReferenceNumber != sourceOperation.ReferenceNumber)
            return new(InvoiceApiDecision.Conflict, "predecessor_not_accepted");

        var document = await db.InvoiceDocuments.AsNoTracking()
            .Where(x => x.Id == successor && x.CustomerUserId == owner &&
                x.TaxpayerId == taxpayerId && x.Subject == InvoiceSubject.Correction &&
                x.State == InvoiceLifecycleState.Finalized)
            .Select(x => x.SnapshotJson)
            .SingleOrDefaultAsync(ct);
        if (document is null)
            return new(InvoiceApiDecision.Conflict, "correction_successor_invalid");

        string? referenceTaxId;
        try
        {
            using var snapshot = JsonDocument.Parse(document);
            referenceTaxId = snapshot.RootElement.GetProperty("referenceTaxId").GetString();
            if (snapshot.RootElement.GetProperty("environment").GetString() != "Production" ||
                referenceTaxId is not { Length: 22 })
                return new(InvoiceApiDecision.Conflict, "correction_successor_invalid");
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return new(InvoiceApiDecision.Conflict, "correction_successor_invalid");
        }

        var relationVerified = await db.Database.SqlQuery<int>($"""
            SELECT 1 AS "Value" FROM authority_invoice_legal_relations
            WHERE customer_user_id={owner} AND taxpayer_id={taxpayerId}
             AND predecessor_invoice_id={predecessorId} AND successor_invoice_id={successor}
             AND predecessor_operation_id={sourceOperation.OperationId}
             AND predecessor_fiscal_tax_id={referenceTaxId}
             AND subject=2 AND state IN (1,2,4)
            LIMIT 1
            """).AnyAsync(ct);
        if (!relationVerified)
            return new(InvoiceApiDecision.Conflict, "correction_relation_not_verified");

        var existingDispatch = await db.Database.SqlQuery<int>($"""
            SELECT 1 AS "Value" FROM invoice_dispatch_operations
            WHERE customer_user_id={owner} AND taxpayer_id={taxpayerId} AND invoice_id={successor}
            LIMIT 1
            """).AnyAsync(ct);
        var existingFiscalReservation = await db.Database.SqlQuery<int>($"""
            SELECT 1 AS "Value" FROM authority_invoice_fiscal_reservations
            WHERE customer_user_id={owner} AND taxpayer_id={taxpayerId} AND invoice_id={successor}
            LIMIT 1
            """).AnyAsync(ct);
        if (!CanResumeUnpreparedSuccessor(productionRelation: true,
            activeRelation: relationVerified, existingDispatch, existingFiscalReservation))
            return new(InvoiceApiDecision.Conflict, "correction_retry_requires_reconciliation");

        var result = await sender.SendAsync(bearer, taxpayerId, successor, ct);
        return new(result.Decision, result.Code, Draft: null, InvoiceId: successor,
            Status: result.Status, ReferenceNumber: result.ReferenceNumber,
            InquiryCode: result.InquiryCode);
    }

    internal async Task<CorrectionOperationResult?> PersistSuccessorAsync(
        ProductionCorrectionSuccessorWrite write,
        CancellationToken ct)
    {
        var owner = write.Invoice.CustomerUserId;
        var taxpayerId = write.Invoice.TaxpayerId;
        var invoiceId = write.Invoice.Id;
        var now = write.Invoice.UpdatedAtUtc;
        try
        {
            // Only the local transaction is retried. External submission happens
            // once, after this method has confirmed the commit.
            return await db.Database.CreateExecutionStrategy().ExecuteAsync<CorrectionOperationResult?>(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
                var updated = await db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE authority_invoice_correction_workspaces SET state=2,updated_at_utc={now}
                    WHERE id={write.WorkspaceId} AND revision={write.ExpectedRevision} AND state=1
                     AND customer_user_id={owner} AND taxpayer_id={taxpayerId}
                    """, ct);
                if (updated != 1)
                {
                    return new(InvoiceApiDecision.Conflict, "draft_changed");
                }
                // Reuse the same entity instances across attempts. Add marks them as
                // pending again if a later command rolled back a successful SaveChanges.
                db.InvoiceDocuments.Add(write.Invoice);
                db.InvoiceDocumentRevisions.Add(write.Revision);
                db.InvoiceReviews.Add(write.Review);
                await db.SaveChangesAsync(ct);
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO authority_invoice_legal_relations
                    (id,customer_user_id,taxpayer_id,predecessor_invoice_id,successor_invoice_id,
                    predecessor_fiscal_tax_id,predecessor_operation_id,subject,state,idempotency_key,evidence_reference,created_at_utc)
                    VALUES({Guid.NewGuid()},{owner},{taxpayerId},{write.PredecessorInvoiceId},{invoiceId},
                     {write.ReferenceTaxId},{write.PredecessorOperationId},2,1,{"correction:" + write.WorkspaceId.ToString("N")},{"accepted_inquiry:" + write.PredecessorOperationId.ToString("N")},{now})
                    """, ct);
                await tx.CommitAsync(ct);
                return null;
            });
        }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure)
        {
            return new(InvoiceApiDecision.Conflict, "correction_conflict");
        }
    }

    private async Task<Guid?> ResolveOwnerAsync(
        string? bearer,
        Guid taxpayerId,
        CancellationToken ct)
    {
        var actor = await scope.ResolveAsync(bearer, ct);
        if (actor.Decision != TaxpayerScopeDecision.Allowed)
        {
            return null;
        }

        return await scope.ScopedQuery(actor.ActorId, actor.Level1)
            .Where(taxpayer => taxpayer.Id == taxpayerId)
            .Select(taxpayer => (Guid?)taxpayer.CustomerUserId)
            .SingleOrDefaultAsync(ct);
    }

    private async Task<(Guid Id, long Revision, string Json)?> FindActiveDraftAsync(
        Guid owner,
        Guid taxpayer,
        Guid predecessor,
        CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != System.Data.ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(ct);
        }
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT id,revision,draft_json::text FROM authority_invoice_correction_workspaces
                WHERE customer_user_id=@owner AND taxpayer_id=@taxpayer
                 AND predecessor_invoice_id=@predecessor AND state=1
                ORDER BY created_at_utc DESC LIMIT 1
                """;
            foreach (var (name, value) in new (string, object)[]
            {
                ("owner", owner),
                ("taxpayer", taxpayer),
                ("predecessor", predecessor)
            })
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = name;
                parameter.Value = value;
                command.Parameters.Add(parameter);
            }
            await using var reader = await command.ExecuteReaderAsync(ct);
            return await reader.ReadAsync(ct)
                ? (reader.GetGuid(0), reader.GetInt64(1), reader.GetString(2))
                : null;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static CorrectionDraftView CreateDraftView(
        Guid workspaceId,
        Guid predecessorInvoiceId,
        long revision,
        string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var lines = JsonSerializer.Deserialize<InvoiceEntryLine[]>(root.GetProperty("lines").GetRawText()) ?? [];
        return new(
            workspaceId,
            predecessorInvoiceId,
            revision,
            lines,
            root.GetProperty("settlementMethod").GetInt32(),
            root.GetProperty("cashAmountRial").GetDecimal(),
            root.GetProperty("creditAmountRial").GetDecimal(),
            root.TryGetProperty("correctionBaselineQuantities", out var baselineQuantities)
                ? JsonSerializer.Deserialize<decimal[]>(baselineQuantities.GetRawText())
                : lines.Select(x => x.Quantity).ToArray());
    }
}
