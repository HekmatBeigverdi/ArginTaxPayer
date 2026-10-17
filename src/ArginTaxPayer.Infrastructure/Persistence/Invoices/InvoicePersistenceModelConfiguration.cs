using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public static class InvoicePersistenceModelConfiguration
{
    public static void Apply(ModelBuilder b)
    {
        var invoice = b.Entity<StoredInvoice>();
        invoice.ToTable("invoice_documents", t =>
        {
            t.HasCheckConstraint("ck_invoice_documents_revision", "revision > 0");
            t.HasCheckConstraint("ck_invoice_documents_state", "state IN (1,2,3)");
            t.HasCheckConstraint("ck_invoice_documents_subject", "subject IN (1,2,3,4)");
            t.HasCheckConstraint("ck_invoice_documents_hash", "snapshot_sha256 ~ '^[0-9a-f]{64}$'");
        });
        invoice.HasKey(x => x.Id);
        invoice.Property(x => x.Id).HasColumnName("id");
        invoice.Property(x => x.CustomerUserId).HasColumnName("customer_user_id");
        invoice.Property(x => x.TaxpayerId).HasColumnName("taxpayer_id");
        invoice.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
        invoice.Property(x => x.State).HasColumnName("state").HasConversion<int>();
        invoice.Property(x => x.Subject).HasColumnName("subject").HasConversion<int>();
        invoice.Property(x => x.IssueDateUtc).HasColumnName("issue_date_utc").HasColumnType("timestamp with time zone");
        invoice.Property(x => x.SnapshotJson).HasColumnName("snapshot_json").HasColumnType("jsonb").IsRequired();
        invoice.Property(x => x.SnapshotSha256).HasColumnName("snapshot_sha256").HasMaxLength(64).IsRequired();
        invoice.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");
        invoice.HasOne<IdentityUser>().WithMany()
            .HasForeignKey(x => x.CustomerUserId).OnDelete(DeleteBehavior.Restrict);
        invoice.HasOne<TaxpayerProfile>().WithMany()
            .HasForeignKey(x => x.TaxpayerId).OnDelete(DeleteBehavior.Restrict);
        invoice.HasIndex(x => new { x.CustomerUserId, x.TaxpayerId, x.Id }).IsUnique().HasDatabaseName("ux_invoice_documents_scope");
        invoice.HasIndex(x => new { x.CustomerUserId, x.TaxpayerId, x.IssueDateUtc }).HasDatabaseName("ix_invoice_documents_scope_date");

        var operation = b.Entity<StoredInvoiceOperation>();
        operation.ToTable("invoice_operations", t =>
        {
            t.HasCheckConstraint("ck_invoice_operations_revision", "expected_revision > 0");
            t.HasCheckConstraint("ck_invoice_operations_subject", "subject IN (1,2,3,4)");
            t.HasCheckConstraint("ck_invoice_operations_fingerprint", "fingerprint_sha256 ~ '^[0-9a-f]{64}$'");
            t.HasCheckConstraint("ck_invoice_operations_slot", "(subject=1 AND reference_tax_id IS NULL AND reference_slot=0) OR (subject IN (2,4) AND reference_tax_id ~ '^[A-Za-z0-9]{22}$' AND reference_slot=1) OR (subject=3 AND reference_tax_id ~ '^[A-Za-z0-9]{22}$' AND reference_slot=2)");
        });
        operation.HasKey(x => x.Id);
        operation.Property(x => x.Id).HasColumnName("id");
        operation.Property(x => x.CustomerUserId).HasColumnName("customer_user_id");
        operation.Property(x => x.TaxpayerId).HasColumnName("taxpayer_id");
        operation.Property(x => x.InvoiceId).HasColumnName("invoice_id");
        operation.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(128).IsRequired();
        operation.Property(x => x.FingerprintSha256).HasColumnName("fingerprint_sha256").HasMaxLength(64).IsRequired();
        operation.Property(x => x.ExpectedRevision).HasColumnName("expected_revision");
        operation.Property(x => x.Subject).HasColumnName("subject").HasConversion<int>();
        operation.Property(x => x.ReferenceTaxId).HasColumnName("reference_tax_id").HasMaxLength(22);
        operation.Property(x => x.ReferenceSlot).HasColumnName("reference_slot");
        operation.Property(x => x.IsClaimActive).HasColumnName("is_claim_active");
        operation.Property(x => x.OutcomeJson).HasColumnName("outcome_json").HasColumnType("jsonb").IsRequired();
        operation.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        operation.HasOne<StoredInvoice>().WithMany()
            .HasForeignKey(x => new { x.CustomerUserId, x.TaxpayerId, x.InvoiceId })
            .HasPrincipalKey(x => new { x.CustomerUserId, x.TaxpayerId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        operation.HasOne<IdentityUser>().WithMany()
            .HasForeignKey(x => x.CustomerUserId).OnDelete(DeleteBehavior.Restrict);
        operation.HasOne<TaxpayerProfile>().WithMany()
            .HasForeignKey(x => x.TaxpayerId).OnDelete(DeleteBehavior.Restrict);
        operation.HasIndex(x => new { x.CustomerUserId, x.TaxpayerId, x.IdempotencyKey })
            .IsUnique().HasDatabaseName("ux_invoice_operations_idempotency");
        operation.HasIndex(x => new { x.CustomerUserId, x.TaxpayerId, x.ReferenceTaxId, x.ReferenceSlot })
            .IsUnique().HasFilter("is_claim_active AND reference_tax_id IS NOT NULL")
            .HasDatabaseName("ux_invoice_operations_active_reference");

        var history = b.Entity<StoredInvoiceRevision>();
        history.ToTable("invoice_document_revisions", t =>
        {
            t.HasCheckConstraint("ck_invoice_history_revision", "revision > 0");
            t.HasCheckConstraint("ck_invoice_history_hash", "snapshot_sha256 ~ '^[0-9a-f]{64}
        cursor.ToTable("invoice_reservation_cursors", t =>
            t.HasCheckConstraint("ck_invoice_cursor_revision", "revision >= 0"));
        cursor.HasKey(x => new { x.CustomerUserId, x.TaxpayerId });
        cursor.Property(x => x.CustomerUserId).HasColumnName("customer_user_id");
        cursor.Property(x => x.TaxpayerId).HasColumnName("taxpayer_id");
        cursor.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
        cursor.HasOne<IdentityUser>().WithMany()
            .HasForeignKey(x => x.CustomerUserId).OnDelete(DeleteBehavior.Restrict);
        cursor.HasOne<TaxpayerProfile>().WithMany()
            .HasForeignKey(x => x.TaxpayerId).OnDelete(DeleteBehavior.Restrict);

        // Constraints prevent accidental deletion of historical invoice data.
        // Application permissions still MUST be checked by authorized caller.
    }
}
");
        });
        history.HasKey(x => new { x.InvoiceId, x.Revision });
        history.Property(x => x.InvoiceId).HasColumnName("invoice_id");
        history.Property(x => x.CustomerUserId).HasColumnName("customer_user_id");
        history.Property(x => x.TaxpayerId).HasColumnName("taxpayer_id");
        history.Property(x => x.Revision).HasColumnName("revision");
        history.Property(x => x.SnapshotJson).HasColumnName("snapshot_json").HasColumnType("jsonb").IsRequired();
        history.Property(x => x.SnapshotSha256).HasColumnName("snapshot_sha256").HasMaxLength(64).IsRequired();
        history.Property(x => x.CapturedAtUtc).HasColumnName("captured_at_utc").HasColumnType("timestamp with time zone");
        history.HasOne<StoredInvoice>().WithMany()
            .HasForeignKey(x => new { x.CustomerUserId, x.TaxpayerId, x.InvoiceId })
            .HasPrincipalKey(x => new { x.CustomerUserId, x.TaxpayerId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        history.HasIndex(x => new { x.CustomerUserId, x.TaxpayerId, x.InvoiceId, x.Revision })
            .HasDatabaseName("ix_invoice_history_scope");

        var cursor = b.Entity<StoredInvoiceReservationCursor>();
        cursor.ToTable("invoice_reservation_cursors", t =>
            t.HasCheckConstraint("ck_invoice_cursor_revision", "revision >= 0"));
        cursor.HasKey(x => new { x.CustomerUserId, x.TaxpayerId });
        cursor.Property(x => x.CustomerUserId).HasColumnName("customer_user_id");
        cursor.Property(x => x.TaxpayerId).HasColumnName("taxpayer_id");
        cursor.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();

        // Constraints prevent accidental deletion of historical invoice data.
        // Application permissions still MUST be checked by authorized caller.
    }
}
