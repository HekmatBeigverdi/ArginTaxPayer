using System.Data.Common;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10CorrectionPersistenceTests
{
    private sealed class FailFirstRelationInsert : DbCommandInterceptor
    {
        public int Failures { get; private set; }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Failures == 0 && command.CommandText.Contains("INSERT INTO sandbox_invoice_legal_relations"))
            {
                Failures++;
                throw new PostgresException("Synthetic serialization failure", "ERROR", "ERROR", "40001");
            }
            return ValueTask.FromResult(result);
        }
    }

    [Phase09PostgresFact]
    public async Task Transient_failure_retries_only_persistence_and_commits_one_successor()
    {
        var interceptor = new FailFirstRelationInsert();
        await using var db = await TemporaryDatabase(interceptor);
        var write = NewWrite();
        await InsertDraft(db, write);

        Assert.Null(await Service(db).PersistSuccessorAsync(write, CancellationToken.None));
        Assert.Equal(1, interceptor.Failures);
        Assert.Equal(1, await db.InvoiceDocuments.CountAsync());
        Assert.Equal(1, await db.InvoiceDocumentRevisions.CountAsync());
        Assert.Equal(1, await db.InvoiceReviews.CountAsync());
        Assert.Equal(2, await db.Database.SqlQueryRaw<int>(
            "SELECT state AS \"Value\" FROM sandbox_invoice_correction_workspaces").SingleAsync());
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>(
            "SELECT count(*)::int AS \"Value\" FROM sandbox_invoice_legal_relations").SingleAsync());
    }

    [Phase09PostgresFact]
    public async Task Failed_relation_insert_rolls_back_draft_claim_and_all_successor_rows()
    {
        await using var db = await TemporaryDatabase();
        var write = NewWrite() with { ReferenceTaxId = "invalid" };
        await InsertDraft(db, write);

        var error = await Assert.ThrowsAsync<PostgresException>(() => Service(db)
            .PersistSuccessorAsync(write, CancellationToken.None));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.False(await db.InvoiceDocuments.AsNoTracking().AnyAsync());
        Assert.False(await db.InvoiceDocumentRevisions.AsNoTracking().AnyAsync());
        Assert.False(await db.InvoiceReviews.AsNoTracking().AnyAsync());
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>(
            "SELECT state AS \"Value\" FROM sandbox_invoice_correction_workspaces").SingleAsync());
    }

    private static Phase10SandboxCorrectionService Service(ArginTaxPayerDbContext db) =>
        new(db, null!, null!, null!, new ConfigurationBuilder().Build(), null!);

    private static CorrectionSuccessorWrite NewWrite()
    {
        var owner = Guid.NewGuid();
        var taxpayer = Guid.NewGuid();
        var invoice = Guid.NewGuid();
        var now = DateTime.UtcNow;
        return new(Guid.NewGuid(), 1, Guid.NewGuid(),
            new StoredInvoice { Id = invoice, CustomerUserId = owner, TaxpayerId = taxpayer,
                Revision = 1, State = InvoiceLifecycleState.Finalized, Subject = InvoiceSubject.Correction,
                IssueDateUtc = now, SnapshotJson = "{}", SnapshotSha256 = new string('a', 64), UpdatedAtUtc = now },
            new StoredInvoiceRevision { InvoiceId = invoice, CustomerUserId = owner, TaxpayerId = taxpayer,
                Revision = 1, SnapshotJson = "{}", SnapshotSha256 = new string('a', 64), CapturedAtUtc = now },
            new StoredInvoiceReview { InvoiceId = invoice, CustomerUserId = owner, TaxpayerId = taxpayer,
                Revision = 1, State = InvoiceReviewState.Approved, LastActorUserId = owner, UpdatedAtUtc = now },
            "ABC1230000000000000001");
    }

    private static async Task<ArginTaxPayerDbContext> TemporaryDatabase(IInterceptor? interceptor = null)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")
            ?? throw new InvalidOperationException("Explicit test DB configuration is required.");
        if (new NpgsqlConnectionStringBuilder(connection).Database != "argintaxpayer_test")
            throw new InvalidOperationException("Refusing non-test database.");
        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql(connection, postgres => postgres.EnableRetryOnFailure(3));
        if (interceptor is not null) options.AddInterceptors(interceptor);
        var db = new ArginTaxPayerDbContext(options.Options);
        await db.Database.OpenConnectionAsync();
        // All writes stay in connection-local shadow tables. Real invoices and
        // append-only journals are untouched; disposing the connection drops them.
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TEMP TABLE invoice_documents (LIKE public.invoice_documents INCLUDING ALL);
            CREATE TEMP TABLE invoice_document_revisions (LIKE public.invoice_document_revisions INCLUDING ALL);
            CREATE TEMP TABLE invoice_reviews (LIKE public.invoice_reviews INCLUDING ALL);
            CREATE TEMP TABLE sandbox_invoice_correction_workspaces (LIKE public.sandbox_invoice_correction_workspaces INCLUDING ALL);
            CREATE TEMP TABLE sandbox_invoice_legal_relations (LIKE public.sandbox_invoice_legal_relations INCLUDING ALL);
            """);
        return db;
    }

    private static Task<int> InsertDraft(ArginTaxPayerDbContext db, CorrectionSuccessorWrite write) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO sandbox_invoice_correction_workspaces
            (id,customer_user_id,taxpayer_id,predecessor_invoice_id,predecessor_fiscal_tax_id,
             revision,draft_json,state,created_at_utc,updated_at_utc)
            VALUES ({write.WorkspaceId},{write.Invoice.CustomerUserId},{write.Invoice.TaxpayerId},
             {write.PredecessorInvoiceId},{"ABC1230000000000000001"},1,CAST({"{}"} AS jsonb),1,{DateTime.UtcNow},{DateTime.UtcNow})
            """);
}
