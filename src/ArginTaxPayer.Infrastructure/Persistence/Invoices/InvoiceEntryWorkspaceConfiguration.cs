using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public static class InvoiceEntryWorkspaceConfiguration
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        var history = modelBuilder.Entity<InvoiceEntryWorkspaceRevision>();
        history.ToTable("invoice_entry_workspace_revisions");
        history.HasKey(h=>new {h.WorkspaceId,h.Revision});
        history.Property(h=>h.WorkspaceId).HasColumnName("workspace_id");
        history.Property(h=>h.CustomerUserId).HasColumnName("customer_user_id");
        history.Property(h=>h.TaxpayerId).HasColumnName("taxpayer_id");
        history.Property(h=>h.Revision).HasColumnName("revision");
        history.Property(h=>h.ActorUserId).HasColumnName("actor_user_id");
        history.Property(h=>h.Action).HasColumnName("action").HasMaxLength(16);
        history.Property(h=>h.Reason).HasColumnName("reason").HasMaxLength(1000);
        history.Property(h=>h.SnapshotJson).HasColumnName("snapshot_json").HasColumnType("jsonb");
        history.Property(h=>h.OccurredAtUtc).HasColumnName("occurred_at_utc").HasColumnType("timestamp with time zone");
        history.HasIndex(h=>new {h.CustomerUserId,h.TaxpayerId,h.WorkspaceId,h.Revision})
            .HasDatabaseName("ix_invoice_workspace_history_scope");
        var e = modelBuilder.Entity<InvoiceEntryWorkspace>();
        e.ToTable("invoice_entry_workspaces", table =>
        {
            table.HasCheckConstraint("ck_invoice_entry_workspace_revision","revision > 0");
            table.HasCheckConstraint("ck_invoice_entry_workspace_step","last_completed_step IN (0,1,2)");
        });
        e.HasKey(x=>x.Id);
        e.Property(x=>x.Id).HasColumnName("id");
        e.Property(x=>x.CustomerUserId).HasColumnName("customer_user_id");
        e.Property(x=>x.TaxpayerId).HasColumnName("taxpayer_id");
        e.Property(x=>x.Revision).HasColumnName("revision").IsConcurrencyToken();
        e.Property(x=>x.LastCompletedStep).HasColumnName("last_completed_step");
        e.Property(x=>x.IssueDateUtc).HasColumnName("issue_date_utc").HasColumnType("timestamp with time zone");
        e.Property(x=>x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");
        e.Property(x=>x.DraftJson).HasColumnName("draft_json").HasColumnType("jsonb").IsRequired();
        e.HasOne<ArginTaxPayer.Infrastructure.Persistence.Taxpayers.TaxpayerProfile>().WithMany().HasForeignKey(x=>x.TaxpayerId)
            .OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ArginTaxPayer.Infrastructure.Persistence.Identity.IdentityUser>().WithMany().HasForeignKey(x=>x.CustomerUserId)
            .OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x=>new{x.CustomerUserId,x.TaxpayerId,x.UpdatedAtUtc})
            .HasDatabaseName("ix_invoice_entry_workspaces_scope");
    }
}