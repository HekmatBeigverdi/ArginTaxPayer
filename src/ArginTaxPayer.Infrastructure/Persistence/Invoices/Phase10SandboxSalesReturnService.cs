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

public sealed record SalesReturnEditLine(
    decimal Quantity,
    decimal UnitPriceRial,
    decimal DiscountPerUnitRial);

public sealed record SalesReturnSaveRequest(
    long ExpectedRevision,
    SalesReturnEditLine[] Lines,
    int SettlementMethod,
    decimal CashAmountRial,
    decimal CreditAmountRial,
    bool ReductionCorrectsOriginalMistake = false,
    string? ReductionEvidenceReference = null);

public sealed record SalesReturnDraftView(
    Guid WorkspaceId,
    Guid PredecessorInvoiceId,
    long Revision,
    InvoiceEntryLine[] Lines,
    int SettlementMethod,
    decimal CashAmountRial,
    decimal CreditAmountRial);

public sealed record SalesReturnOperationResult(
    InvoiceApiDecision Decision,
    string Code,
    SalesReturnDraftView? Draft = null,
    Guid? InvoiceId = null,
    string? Status = null,
    string? ReferenceNumber = null,
    string? InquiryCode = null);

internal sealed record SalesReturnSuccessorWrite(
    Guid WorkspaceId,
    long ExpectedRevision,
    Guid PredecessorInvoiceId,
    StoredInvoice Invoice,
    StoredInvoiceRevision Revision,
    StoredInvoiceReview Review,
    string ReferenceTaxId,
    InvoiceEntryLine[] PredecessorLines);

/// <summary>Independent V7.9 sales return draft, immutable predecessor, one send operation.</summary>
public sealed class Phase10SandboxSalesReturnService(
    ArginTaxPayerDbContext db,
    TaxpayerScopeService scope,
    Phase10LegacyReferenceInquiryService inquiry,
    Phase10OneClickSandboxSubmissionService sender,
    IConfiguration config,
    LegacyAuthorityReportRepository authorityReports)
{
    public async Task<SalesReturnOperationResult> OpenAsync(
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
            return new(
                InvoiceApiDecision.Success,
                "existing_draft",
                CreateDraftView(existingDraft.Id, predecessorId, existingDraft.Revision, existingDraft.Json));
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

        var official = await inquiry.ReadAsync(bearer, taxpayerId, predecessorId, ct);
        if (official.Decision != InvoiceApiDecision.Success || official.AuthorityResult?.Status != "SUCCESS")
        {
            return new(InvoiceApiDecision.Conflict, "accepted_predecessor_required");
        }

        var referenceTaxId = await db.Database.SqlQueryRaw<string>($"""
            SELECT fiscal_tax_id AS "Value" FROM sandbox_invoice_fiscal_reservations
            WHERE customer_user_id='{owner.Value}' AND taxpayer_id='{taxpayerId}'
             AND invoice_id='{predecessorId}' LIMIT 1
            """).FirstOrDefaultAsync(ct);
        if (referenceTaxId is not { Length: 22 })
        {
            return new(InvoiceApiDecision.Conflict, "reference_taxid_missing");
        }
        if (await db.Database.SqlQueryRaw<int>($"""
            SELECT 1 AS "Value" FROM sandbox_invoice_legal_relations
            WHERE customer_user_id='{owner.Value}' AND taxpayer_id='{taxpayerId}'
             AND predecessor_invoice_id='{predecessorId}' AND subject IN (2,3)
             AND state IN (1,2,4) LIMIT 1
            """).AnyAsync(ct))
        {
            return new(InvoiceApiDecision.Conflict, "active_related_invoice");
        }

        var snapshot = JsonNode.Parse(original.SnapshotJson)?.AsObject();
        if (snapshot is null || (string?)snapshot["environment"] != "Sandbox" ||
            (int?)snapshot["invoiceType"] != 1 || (int?)snapshot["pattern"] != 1)
        {
            return new(InvoiceApiDecision.Invalid, "unsupported_pattern");
        }

        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        snapshot["referenceTaxId"] = referenceTaxId;
        snapshot["predecessorInvoiceId"] = predecessorId.ToString("D");
        snapshot["legalSubject"] = 2;
        var json = snapshot.ToJsonString();
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO sandbox_invoice_sales_return_workspaces
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

    public async Task<SalesReturnOperationResult> ReadAsync(
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

        return new(
            InvoiceApiDecision.Success,
            "draft_loaded",
            CreateDraftView(savedDraft.Id, predecessorId, savedDraft.Revision, savedDraft.Json));
    }

    public async Task<SalesReturnOperationResult> SaveAsync(
        string? bearer,
        Guid taxpayerId,
        Guid predecessorId,
        SalesReturnSaveRequest input,
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
        if (false && input.ReductionCorrectsOriginalMistake &&
            (string.IsNullOrWhiteSpace(input.ReductionEvidenceReference) || input.ReductionEvidenceReference.Length > 256))
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
        if (input.Lines.Length != baseline.Length ||
            !Phase10SalesReturnValidation.AllowedLines(baseline, edited, new decimal[baseline.Length]))
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
            financial = InvoiceFinancialCalculator.Preview(edited.Where(x => x.Quantity > 0).Select(x => new InvoiceFinancialInput(
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
        var otherTaxAndLegalFees = 0m;
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
        root["settlementMethod"] = input.SettlementMethod;
        root["cashAmountRial"] = cashAmount;
        root["creditAmountRial"] = creditAmount;
        root["financialSummary"] = JsonSerializer.SerializeToNode(new InvoiceDraftFinancialSummary(
            financial.BeforeDiscountRial, financial.DiscountRial, financial.AfterDiscountRial,
            financial.VatRial ?? 0m, financial.GrandTotalRial ?? 0m));
        root["reductionCorrectsOriginalMistake"] = input.ReductionCorrectsOriginalMistake;
        root["reductionEvidenceReference"] = input.ReductionCorrectsOriginalMistake ? input.ReductionEvidenceReference : null;
        var json = root.ToJsonString();
        var now = DateTime.UtcNow;
        var rows = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE sandbox_invoice_sales_return_workspaces
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

    public async Task<SalesReturnOperationResult> DiscardAsync(
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
            UPDATE sandbox_invoice_sales_return_workspaces SET state=3,updated_at_utc={DateTime.UtcNow}
            WHERE customer_user_id={owner.Value} AND taxpayer_id={taxpayerId}
             AND predecessor_invoice_id={predecessorId} AND state=1
            """, ct);
        return new(InvoiceApiDecision.Success, rows > 0 ? "draft_discarded" : "nothing_to_discard");
    }

    public async Task<SalesReturnOperationResult> SubmitAsync(
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

        var draft = await FindActiveDraftAsync(owner.Value, taxpayerId, predecessorId, ct);
        if (draft is not { } savedDraft)
        {
            return new(InvoiceApiDecision.NotFound, "draft_not_found");
        }

        var accepted = await inquiry.ReadAsync(bearer, taxpayerId, predecessorId, ct);
        if (accepted.AuthorityResult?.Status != "SUCCESS")
        {
            return new(InvoiceApiDecision.Conflict, "predecessor_not_accepted");
        }

        var original = await db.InvoiceDocuments.AsNoTracking().FirstAsync(x =>
            x.Id == predecessorId && x.TaxpayerId == taxpayerId && x.CustomerUserId == owner.Value, ct);
        var root = JsonNode.Parse(savedDraft.Json)!.AsObject();
        using var origDoc = JsonDocument.Parse(original.SnapshotJson);
        var originalLines = JsonSerializer.Deserialize<InvoiceEntryLine[]>(
            origDoc.RootElement.GetProperty("lines").GetRawText()) ?? [];
        var changedLines = JsonSerializer.Deserialize<InvoiceEntryLine[]>(
            root["lines"]!.ToJsonString()) ?? [];
        var reductionCorrectsOriginalMistake = (bool?)root["reductionCorrectsOriginalMistake"] ?? false;
        if (!Phase10SalesReturnValidation.AllowedLines(originalLines, changedLines, new decimal[originalLines.Length]))
        {
            return new(InvoiceApiDecision.Invalid, "invalid_correction_lines");
        }

        var linesChanged = originalLines.Zip(changedLines).Any(pair =>
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

        if (false && reductionCorrectsOriginalMistake && string.IsNullOrWhiteSpace((string?)root["reductionEvidenceReference"]))
        {
            return new(InvoiceApiDecision.Invalid, "original_mistake_evidence_required");
        }
        if (config["SandboxInvoiceLegacy:Enabled"] != "true" ||
            config["SandboxInvoiceLegacy:OperatorApprovedEndpoint"] != "true")
        {
            return new(InvoiceApiDecision.Invalid, "sandbox_send_disabled");
        }

        var database = await db.Database.SqlQueryRaw<string>("SELECT current_database() AS \"Value\"").SingleAsync(ct);
        if (database != "argintaxpayer_test")
        {
            return new(InvoiceApiDecision.Forbidden, "sandbox_database_required");
        }

        // Create the successor without modifying the accepted predecessor.
        var now = DateTime.UtcNow;
        var invoiceId = Guid.NewGuid();
        root["sourceWorkspaceId"] = invoiceId.ToString("D");
        root["sourceRevision"] = 1;
        root["issueDateUtc"] = now;
        root["legalSubject"] = 4;
        var json = root.ToJsonString();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
        var successor = new StoredInvoice
        {
            Id = invoiceId,
            CustomerUserId = owner.Value,
            TaxpayerId = taxpayerId,
            Revision = 1,
            State = InvoiceLifecycleState.Finalized,
            Subject = InvoiceSubject.SalesReturn,
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
        var write = new SalesReturnSuccessorWrite(
            savedDraft.Id, savedDraft.Revision, predecessorId,
            successor, successorRevision, successorReview, (string?)root["referenceTaxId"] ?? "", originalLines);
        var persistenceResult = await PersistSuccessorAsync(write, ct);
        if (persistenceResult is not null)
        {
            return persistenceResult;
        }

        // Send only after the successor, revision, review and legal relation commit together.
        var result = await sender.SendAsync(bearer, taxpayerId, invoiceId, ct);
        var report = await authorityReports.ReadAsync(owner.Value, taxpayerId, invoiceId, ct);
        if (report?.Report.Status is "SUCCESS" or "FAILED")
        {
            var succeeded = report.Report.Status == "SUCCESS";
            var evidence = succeeded ? report.ReferenceNumber :
                "authority_failed:" + (report.Report.Errors.FirstOrDefault()?.Code ?? "unknown");
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE sandbox_invoice_legal_relations
                SET state={(succeeded ? 2 : 3)},evidence_reference={evidence},
                 confirmed_at_utc={(succeeded ? (DateTime?)DateTime.UtcNow : null)}
                WHERE customer_user_id={owner.Value} AND taxpayer_id={taxpayerId}
                 AND successor_invoice_id={invoiceId} AND subject=4 AND state=1
                """, ct);
        }

        return new(
            result.Decision,
            result.Code,
            Draft: null,
            InvoiceId: invoiceId,
            Status: result.Status,
            ReferenceNumber: result.ReferenceNumber,
            InquiryCode: result.InquiryCode);
    }

    internal async Task<SalesReturnOperationResult?> PersistSuccessorAsync(
        SalesReturnSuccessorWrite write,
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
            return await db.Database.CreateExecutionStrategy().ExecuteAsync<SalesReturnOperationResult?>(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
                // Serialize all returns of the same predecessor before reserving quantities.
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE invoice_documents SET updated_at_utc=updated_at_utc
                    WHERE id={write.PredecessorInvoiceId} AND taxpayer_id={taxpayerId}
                      AND customer_user_id={owner}
                    """, ct);
                using var returnDocument = JsonDocument.Parse(write.Invoice.SnapshotJson);
                var returnLines = JsonSerializer.Deserialize<InvoiceEntryLine[]>(
                    returnDocument.RootElement.GetProperty("lines").GetRawText()) ?? [];
                var used = new decimal[write.PredecessorLines.Length];
                for (var i = 0; i < used.Length; i++)
                {
                    var index = i + 1;
                    used[i] = await db.Database.SqlQueryRaw<decimal>($"""
                        SELECT COALESCE(SUM((items.value->>'quantity')::numeric),0) AS "Value"
                        FROM sandbox_invoice_legal_relations rel
                        JOIN invoice_documents successor
                          ON successor.id=rel.successor_invoice_id
                         AND successor.taxpayer_id=rel.taxpayer_id
                         AND successor.customer_user_id=rel.customer_user_id
                        CROSS JOIN LATERAL jsonb_array_elements(successor.snapshot_json::jsonb->'lines')
                          WITH ORDINALITY AS items(value,position)
                        WHERE rel.customer_user_id='{owner}' AND rel.taxpayer_id='{taxpayerId}'
                          AND rel.predecessor_invoice_id='{write.PredecessorInvoiceId}'
                          AND rel.subject=4 AND rel.state IN (1,2,4)
                          AND items.position={index}
                        """).SingleAsync(ct);
                }
                if (!Phase10SalesReturnValidation.AllowedLines(write.PredecessorLines,returnLines,used))
                    return new(InvoiceApiDecision.Conflict,"return_quantity_exceeded");
                var updated = await db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE sandbox_invoice_sales_return_workspaces SET state=2,updated_at_utc={now}
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
                    INSERT INTO sandbox_invoice_legal_relations
                    (id,customer_user_id,taxpayer_id,predecessor_invoice_id,successor_invoice_id,
                    predecessor_fiscal_tax_id,subject,state,idempotency_key,evidence_reference,created_at_utc)
                    VALUES({Guid.NewGuid()},{owner},{taxpayerId},{write.PredecessorInvoiceId},{invoiceId},
                     {write.ReferenceTaxId},4,1,{"return:" + write.WorkspaceId.ToString("N")},{"user-confirmed"},{now})
                    """, ct);
                await tx.CommitAsync(ct);
                return null;
            });
        }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure)
        {
            return new(InvoiceApiDecision.Conflict, "return_conflict");
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
                SELECT id,revision,draft_json::text FROM sandbox_invoice_sales_return_workspaces
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

    private static SalesReturnDraftView CreateDraftView(
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
            root.GetProperty("creditAmountRial").GetDecimal());
    }
}
