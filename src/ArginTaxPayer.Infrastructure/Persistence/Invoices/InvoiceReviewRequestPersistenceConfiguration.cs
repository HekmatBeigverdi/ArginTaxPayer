using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public static class InvoiceReviewRequestPersistenceConfiguration
{
    public static void Apply(ModelBuilder b)
    {
        var e = b.Entity<StoredInvoiceReviewRequest>();
        e.ToTable("invoice_review_requests");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.CustomerUserId).HasColumnName("customer_user_id");
        e.Property(x => x.TaxpayerId).HasColumnName("taxpayer_id");
        e.Property(x => x.InvoiceId).HasColumnName("invoice_id");
        e.Property(x => x.ActorUserId).HasColumnName("actor_user_id");
        e.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(128).IsRequired();
        e.Property(x => x.FingerprintSha256).HasColumnName("fingerprint_sha256").HasMaxLength(64).IsRequired();
        e.Property(x => x.ResultReviewRevision).HasColumnName("result_review_revision");
        e.Property(x => x.ResultState).HasColumnName("result_state");
        e.Property(x => x.CompletedAtUtc).HasColumnName("completed_at_utc").HasColumnType("timestamp with time zone");
        e.HasOne<StoredInvoiceReview>().WithMany()
            .HasForeignKey(x => new { x.CustomerUserId, x.TaxpayerId, x.InvoiceId })
            .HasPrincipalKey(x => new { x.CustomerUserId, x.TaxpayerId, x.InvoiceId })
            .OnDelete(DeleteBehavior.Restrict);
        e.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.CustomerUserId, x.TaxpayerId, x.ActorUserId, x.IdempotencyKey })
            .IsUnique().HasDatabaseName("ux_invoice_review_requests_idempotency");
    }
}
