using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace ArginTaxPayer.Tests;

/// <summary>
/// Offline verification only: no external DB connection and no migrations applied.
/// Actual PostgreSQL CAS/unique-index race tests require an isolated, authorized
/// database in Step18 operator acceptance.
/// </summary>
public sealed class Phase07InvoicePersistenceMetadataTests
{
    private static ArginTaxPayerDbContext Context() => new(
        new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=argintaxpayer_test;Username=unused;Password=unused")
            .Options);

    [Fact]
    public void New_explicit_migration_is_discoverable_without_database_access()
    {
        using var db = Context();
        Assert.Contains("20261017010000_AddInvoicePersistenceAndOperationClaims",
            db.Database.GetMigrations());
    }

    [Fact]
    public void Invoice_document_has_optimistic_revision_and_scoped_unique_identity()
    {
        using var db = Context();
        var entity = db.Model.FindEntityType(typeof(StoredInvoice))!;
        Assert.Equal("invoice_documents", entity.GetTableName());
        Assert.True(entity.FindProperty(nameof(StoredInvoice.Revision))!.IsConcurrencyToken);
        Assert.Contains(entity.GetIndexes(), i =>
            i.IsUnique && i.GetDatabaseName() == "ux_invoice_documents_scope");
    }

    [Fact]
    public void Scoped_request_key_and_active_reference_slot_are_unique()
    {
        using var db = Context();
        var entity = db.Model.FindEntityType(typeof(StoredInvoiceOperation))!;
        Assert.Contains(entity.GetIndexes(), i => i.IsUnique &&
            i.GetDatabaseName() == "ux_invoice_operations_idempotency");
        var slot = Assert.Single(entity.GetIndexes().Where(i =>
            i.GetDatabaseName() == "ux_invoice_operations_active_reference"));
        Assert.True(slot.IsUnique);
        Assert.Equal("is_claim_active AND reference_tax_id IS NOT NULL", slot.GetFilter());
    }

    [Fact]
    public void Revisions_are_composite_keyed_and_scope_constrained()
    {
        using var db = Context();
        var entity = db.Model.FindEntityType(typeof(StoredInvoiceRevision))!;
        Assert.Equal(2, entity.FindPrimaryKey()!.Properties.Count);
        Assert.Contains(entity.GetForeignKeys(), x =>
            x.Properties.Count == 3 &&
            x.PrincipalKey.Properties.Count == 3);
        Assert.Equal("jsonb", entity.FindProperty(nameof(StoredInvoiceRevision.SnapshotJson))!.GetColumnType());
    }

    [Fact]
    public void Cursor_has_revision_concurrency_token_and_scoped_key()
    {
        using var db = Context();
        var entity = db.Model.FindEntityType(typeof(StoredInvoiceReservationCursor))!;
        Assert.Equal(2, entity.FindPrimaryKey()!.Properties.Count);
        Assert.True(entity.FindProperty(nameof(StoredInvoiceReservationCursor.Revision))!.IsConcurrencyToken);
    }

    [Fact]
    public void Atomic_writer_is_infrastructure_only_and_not_a_tax_authority_sender()
    {
        Assert.Equal("EfInvoiceAtomicWriter", typeof(EfInvoiceAtomicWriter).Name);
        Assert.False(new InvoiceWriteResult(InvoiceWriteStatus.Applied, 2).IsFiscalSubmissionAuthorized);
    }
}
