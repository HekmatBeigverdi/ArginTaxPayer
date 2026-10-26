using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using ArginTaxPayer.Domain.Invoices;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public enum Phase10DispatchEvidenceStatus
{
    Recorded = 1,
    StaleOrWrongScope = 2,
    Invalid = 3
}

public sealed record Phase10DispatchEvidenceResult(
    Phase10DispatchEvidenceStatus Status,
    InvoiceDispatchState? NewState = null,
    long? NewStateRevision = null);

/// <summary>
/// Append-only persistence for provider evidence. Initial receipts advance the
/// operation with an exact state-revision CAS. Inquiry evidence is append-only
/// and deliberately does not turn correlation into fiscal acceptance.
/// </summary>
public sealed class Phase10DispatchEvidenceRepository(ArginTaxPayerDbContext db)
{
    public Task<Phase10DispatchEvidenceResult> AppendInitialReceiptAsync(
        Guid operationId, Guid customerUserId, Guid taxpayerId, Guid attemptId,
        long expectedStateRevision, string authorityReference, string evidenceSha256,
        DateTime observedAtUtc, CancellationToken ct = default) =>
        AppendAsync(operationId, customerUserId, taxpayerId, attemptId,
            InvoiceDispatchState.Sending, expectedStateRevision,
            receiptKind: 1, authorityReference, evidenceSha256, observedAtUtc,
            advanceTo: InvoiceDispatchState.Acknowledged, ct);

    public Task<Phase10DispatchEvidenceResult> AppendInquiryEvidenceAsync(
        Guid operationId, Guid customerUserId, Guid taxpayerId, Guid? attemptId,
        long expectedStateRevision, string? authorityReference, string evidenceSha256,
        DateTime observedAtUtc, CancellationToken ct = default) =>
        AppendAsync(operationId, customerUserId, taxpayerId, attemptId,
            InvoiceDispatchState.Indeterminate, expectedStateRevision,
            receiptKind: 2, authorityReference, evidenceSha256, observedAtUtc,
            advanceTo: null, ct);

    public Task<Phase10DispatchEvidenceResult> AppendReferenceInquiryEvidenceAsync(
        Guid operationId, Guid customerUserId, Guid taxpayerId, Guid attemptId,
        long expectedStateRevision, string authorityReference, string evidenceSha256,
        DateTime observedAtUtc, CancellationToken ct = default) =>
        AppendAsync(operationId, customerUserId, taxpayerId, attemptId,
            InvoiceDispatchState.Acknowledged, expectedStateRevision,
            receiptKind: 2, authorityReference, evidenceSha256, observedAtUtc,
            advanceTo: null, ct);

    private async Task<Phase10DispatchEvidenceResult> AppendAsync(
        Guid operationId, Guid customerUserId, Guid taxpayerId, Guid? attemptId,
        InvoiceDispatchState expectedState, long expectedStateRevision,
        int receiptKind, string? authorityReference, string evidenceSha256,
        DateTime observedAtUtc, InvoiceDispatchState? advanceTo, CancellationToken ct)
    {
        if (operationId == Guid.Empty || customerUserId == Guid.Empty ||
            taxpayerId == Guid.Empty || expectedStateRevision < 1 ||
            receiptKind is not (1 or 2) ||
            attemptId is Guid attempt && attempt == Guid.Empty ||
            !ValidReference(authorityReference) || !ValidSha256(evidenceSha256) ||
            observedAtUtc.Kind != DateTimeKind.Utc)
            return new(Phase10DispatchEvidenceStatus.Invalid);

        var connection = db.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened) await connection.OpenAsync(ct);
        try
        {
            await using var tx = await connection.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, ct);
            try
            {
                if ((string?)await Scalar(connection, tx, "SELECT current_database()", ct) !=
                    "argintaxpayer_test")
                    throw new InvalidOperationException("Phase10 evidence requires isolated test DB.");

                await using var lockCommand = Command(connection, tx, """
SELECT state,state_revision
FROM invoice_dispatch_operations
WHERE id=@id AND customer_user_id=@customer AND taxpayer_id=@taxpayer
FOR UPDATE
""", ("id", operationId), ("customer", customerUserId), ("taxpayer", taxpayerId));
                await using var reader = await lockCommand.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct) || reader.GetInt32(0) != (int)expectedState ||
                    reader.GetInt64(1) != expectedStateRevision)
                {
                    await tx.RollbackAsync(CancellationToken.None);
                    return new(Phase10DispatchEvidenceStatus.StaleOrWrongScope);
                }
                await reader.DisposeAsync();

                var receiptId = Guid.NewGuid();
                await using var insert = Command(connection, tx, """
INSERT INTO invoice_dispatch_receipts
(id,operation_id,attempt_id,receipt_kind,authority_reference,evidence_sha256,observed_at_utc)
VALUES (@id,@operation,@attempt,@kind,@reference,@evidence,@observed)
""", ("id", receiptId), ("operation", operationId),
                    ("attempt", attemptId is null ? DBNull.Value : attemptId.Value),
                    ("kind", receiptKind), ("reference", authorityReference is null ? DBNull.Value : authorityReference),
                    ("evidence", evidenceSha256.ToLowerInvariant()), ("observed", observedAtUtc));
                await insert.ExecuteNonQueryAsync(ct);

                if (advanceTo is { } target)
                {
                    await using var update = Command(connection, tx, """
UPDATE invoice_dispatch_operations
SET state=@next,state_revision=state_revision+1,updated_at_utc=@updated
WHERE id=@id AND customer_user_id=@customer AND taxpayer_id=@taxpayer
 AND state=@current AND state_revision=@revision
RETURNING state_revision
""", ("next", (int)target), ("updated", observedAtUtc),
                        ("id", operationId), ("customer", customerUserId),
                        ("taxpayer", taxpayerId), ("current", (int)expectedState),
                        ("revision", expectedStateRevision));
                    var nextRevision = await update.ExecuteScalarAsync(ct);
                    if (nextRevision is not long revision)
                    {
                        await tx.RollbackAsync(CancellationToken.None);
                        return new(Phase10DispatchEvidenceStatus.StaleOrWrongScope);
                    }
                    await tx.CommitAsync(ct);
                    return new(Phase10DispatchEvidenceStatus.Recorded, target, revision);
                }

                await tx.CommitAsync(ct);
                return new(Phase10DispatchEvidenceStatus.Recorded, expectedState,
                    expectedStateRevision);
            }
            catch
            {
                await tx.RollbackAsync(CancellationToken.None);
                throw;
            }
        }
        finally
        {
            if (opened) await connection.CloseAsync();
        }
    }

    public static string EvidenceHash(string safeCode, string? authorityReference,
        DateTime observedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(safeCode) || safeCode.Length > 100 ||
            safeCode.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '.')) ||
            !ValidReference(authorityReference) || observedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Invalid sanitized dispatch evidence.");
        var canonical = $"{safeCode}|{authorityReference ?? ""}|{observedAtUtc:O}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }

    private static bool ValidReference(string? value) => value is null ||
        value is { Length: > 0 and <= 128 } &&
        value.All(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or
            >= '0' and <= '9' or '-' or '_');

    private static bool ValidSha256(string? value) => value is { Length: 64 } &&
        value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or
            >= 'A' and <= 'F');

    private static DbCommand Command(DbConnection connection, DbTransaction tx,
        string sql, params (string Name, object Value)[] values)
    {
        var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = sql;
        foreach (var (name, value) in values)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }
        return command;
    }

    private static async Task<object?> Scalar(DbConnection connection, DbTransaction tx,
        string sql, CancellationToken ct)
    {
        await using var command = Command(connection, tx, sql);
        return await command.ExecuteScalarAsync(ct);
    }
}
