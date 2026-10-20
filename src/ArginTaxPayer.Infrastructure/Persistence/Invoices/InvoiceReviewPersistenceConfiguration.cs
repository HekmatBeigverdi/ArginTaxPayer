using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public static class InvoiceReviewPersistenceConfiguration
{
    public static void Apply(ModelBuilder b)
    {
        var review = b.Entity<StoredInvoiceReview>();
        review.ToTable("invoice_reviews");
        review.HasKey(x => x.InvoiceId);
        review.Property(x => x.InvoiceId).HasColumnName("invoice_id");
        review.Property(x => x.CustomerUserId).HasColumnName("customer_user_id");
        review.Property(x => x.TaxpayerId).HasColumnName("taxpayer_id");
        review.Property(x => x.State).HasColumnName("state").HasConversion<int>();
        review.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
        review.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");
        review.Property(x => x.LastActorUserId).HasColumnName("last_actor_user_id");
        review.HasOne<StoredInvoice>().WithMany()
            .HasForeignKey(x => new { x.CustomerUserId, x.TaxpayerId, x.InvoiceId })
            .HasPrincipalKey(x => new { x.CustomerUserId, x.TaxpayerId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        review.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.LastActorUserId).OnDelete(DeleteBehavior.Restrict);
        review.HasIndex(x => new { x.CustomerUserId, x.TaxpayerId, x.State })
            .HasDatabaseName("ix_invoice_reviews_scope_state");

        var e = b.Entity<StoredInvoiceReviewEvent>();
        e.ToTable("invoice_review_events");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.InvoiceId).HasColumnName("invoice_id");
        e.Property(x => x.CustomerUserId).HasColumnName("customer_user_id");
        e.Property(x => x.TaxpayerId).HasColumnName("taxpayer_id");
        e.Property(x => x.Revision).HasColumnName("revision");
        e.Property(x => x.FromState).HasColumnName("from_state").HasConversion<int>();
        e.Property(x => x.ToState).HasColumnName("to_state").HasConversion<int>();
        e.Property(x => x.Action).HasColumnName("action").HasConversion<int>();
        e.Property(x => x.ActorUserId).HasColumnName("actor_user_id");
        e.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc").HasColumnType("timestamp with time zone");
        e.Property(x => x.ChangeReason).HasColumnName("change_reason").HasMaxLength(1000);
        e.HasOne<StoredInvoiceReview>().WithMany()
            .HasForeignKey(x => new { x.CustomerUserId, x.TaxpayerId, x.InvoiceId })
            .HasPrincipalKey(x => new { x.CustomerUserId, x.TaxpayerId, x.InvoiceId })
            .OnDelete(DeleteBehavior.Restrict);
        e.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.InvoiceId, x.Revision }).IsUnique()
            .HasDatabaseName("ux_invoice_review_events_revision");
        e.HasIndex(x => new { x.CustomerUserId, x.TaxpayerId, x.InvoiceId, x.OccurredAtUtc })
            .HasDatabaseName("ix_invoice_review_events_scope_time");
    }
}
