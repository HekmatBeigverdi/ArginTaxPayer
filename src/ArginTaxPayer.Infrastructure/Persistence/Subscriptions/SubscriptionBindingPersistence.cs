using Microsoft.EntityFrameworkCore;
using ArginTaxPayer.Domain.Subscriptions;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

public sealed class StoredAnnualBinding
{
    public Guid SubscriptionId { get; set; }
    public Guid CustomerUserId { get; set; }
    public Guid TaxpayerId { get; set; }
    public Guid FiscalMemoryId { get; set; }
    public DateTime VerifiedConnectedAtUtc { get; set; }
    public string EvidenceReference { get; set; } = "";
}
public sealed class StoredBindingException
{
    public Guid Id { get; set; }
    public Guid SubscriptionId { get; set; }
    public Guid CustomerUserId { get; set; }
    public Guid RequesterUserId { get; set; }
    public Guid ProposedTaxpayerId { get; set; }
    public Guid ProposedMemoryId { get; set; }
    public string Reason { get; set; } = "";
    public BindingExceptionStatus Status { get; set; } = BindingExceptionStatus.Pending;
    public Guid? ReviewerUserId { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
}
public static class SubscriptionBindingConfiguration
{
    public static void Apply(ModelBuilder builder)
    {
        var b = builder.Entity<StoredAnnualBinding>();
        b.ToTable("subscription_annual_bindings");
        b.HasKey(x => x.SubscriptionId);
        b.Property(x => x.SubscriptionId).HasColumnName("subscription_id");
        b.Property(x => x.CustomerUserId).HasColumnName("customer_user_id");
        b.Property(x => x.TaxpayerId).HasColumnName("taxpayer_id");
        b.Property(x => x.FiscalMemoryId).HasColumnName("fiscal_memory_id");
        b.Property(x => x.VerifiedConnectedAtUtc).HasColumnName("verified_connected_at_utc").HasColumnType("timestamp with time zone");
        b.Property(x => x.EvidenceReference).HasColumnName("evidence_reference").HasMaxLength(128).IsRequired();
        b.HasOne<StoredCustomerSubscription>().WithOne().HasForeignKey<StoredAnnualBinding>(x => x.SubscriptionId).OnDelete(DeleteBehavior.Restrict);

        var e = builder.Entity<StoredBindingException>();
        e.ToTable("subscription_binding_exceptions");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.SubscriptionId).HasColumnName("subscription_id");
        e.Property(x => x.CustomerUserId).HasColumnName("customer_user_id");
        e.Property(x => x.RequesterUserId).HasColumnName("requester_user_id");
        e.Property(x => x.ProposedTaxpayerId).HasColumnName("proposed_taxpayer_id");
        e.Property(x => x.ProposedMemoryId).HasColumnName("proposed_memory_id");
        e.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(1000).IsRequired();
        e.Property(x => x.Status).HasColumnName("status").HasConversion<int>();
        e.Property(x => x.ReviewerUserId).HasColumnName("reviewer_user_id");
        e.Property(x => x.RequestedAtUtc).HasColumnName("requested_at_utc").HasColumnType("timestamp with time zone");
        e.Property(x => x.ReviewedAtUtc).HasColumnName("reviewed_at_utc").HasColumnType("timestamp with time zone");
        e.HasOne<StoredCustomerSubscription>().WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.SubscriptionId, x.RequestedAtUtc });
    }
}
