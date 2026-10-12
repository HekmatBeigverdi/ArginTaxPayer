using ArginTaxPayer.Infrastructure.Persistence.Outbox;
using ArginTaxPayer.Infrastructure.Persistence.Idempotency;
using Microsoft.EntityFrameworkCore;
using ArginTaxPayer.Infrastructure.Persistence.Identity;

namespace ArginTaxPayer.Infrastructure.Persistence;

/// <summary>
/// Explicit migration-managed persistence context. Neither host applies
/// database schema changes automatically.
/// </summary>
public sealed class ArginTaxPayerDbContext(DbContextOptions<ArginTaxPayerDbContext> options)
    : DbContext(options)
{
    public DbSet<IdentityUser> IdentityUsers => Set<IdentityUser>();
    public DbSet<IdentityUserRole> IdentityUserRoles => Set<IdentityUserRole>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<IdempotencyReservation> IdempotencyReservations => Set<IdempotencyReservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var users = modelBuilder.Entity<IdentityUser>();
        users.ToTable("identity_users");
        users.HasKey(x => x.Id);
        users.Property(x => x.Id).HasColumnName("id");
        users.Property(x => x.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(320).IsRequired();
        users.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(512).IsRequired();
        users.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        users.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        users.Property(x => x.PasswordChangedAtUtc).HasColumnName("password_changed_at_utc").HasColumnType("timestamp with time zone");
        users.HasIndex(x => x.NormalizedEmail).IsUnique().HasDatabaseName("ux_identity_users_email");

        var roles = modelBuilder.Entity<IdentityUserRole>();
        roles.ToTable("identity_user_roles");
        roles.HasKey(x => new { x.UserId, x.Role });
        roles.Property(x => x.UserId).HasColumnName("user_id");
        roles.Property(x => x.Role).HasColumnName("role").HasMaxLength(32).IsRequired();
        roles.Property(x => x.AssignedAtUtc).HasColumnName("assigned_at_utc").HasColumnType("timestamp with time zone");
        roles.HasOne(x => x.User).WithMany(x => x.Roles).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        roles.HasIndex(x => x.Role).HasDatabaseName("ix_identity_user_roles_role");

        var reservations = modelBuilder.Entity<IdempotencyReservation>();
        reservations.ToTable("idempotency_reservations");
        reservations.HasKey(x => x.Id);
        reservations.Property(x => x.Id).HasColumnName("id");
        reservations.Property(x => x.TaxpayerId).HasColumnName("taxpayer_id").IsRequired();
        reservations.Property(x => x.Operation).HasColumnName("operation").HasMaxLength(100).IsRequired();
        reservations.Property(x => x.Key).HasColumnName("idempotency_key").HasMaxLength(200).IsRequired();
        reservations.Property(x => x.RequestFingerprint).HasColumnName("request_fingerprint").HasMaxLength(64).IsRequired();
        reservations.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        reservations.HasIndex(x => new { x.TaxpayerId, x.Operation, x.Key })
            .IsUnique().HasDatabaseName("ux_idempotency_taxpayer_operation_key");

        var outbox = modelBuilder.Entity<OutboxMessage>();
        outbox.ToTable("outbox_messages");
        outbox.HasKey(x => x.Id);
        outbox.Property(x => x.Id).HasColumnName("id");
        outbox.Property(x => x.TaxpayerId).HasColumnName("taxpayer_id").IsRequired();
        outbox.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(200).IsRequired();
        outbox.Property(x => x.PayloadJson).HasColumnName("payload_json").HasColumnType("text").IsRequired();
        outbox.Property(x => x.DeduplicationKey).HasColumnName("deduplication_key").HasMaxLength(200).IsRequired();
        outbox.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        outbox.Property(x => x.ProcessedAtUtc).HasColumnName("processed_at_utc").HasColumnType("timestamp with time zone");
        outbox.Property(x => x.AttemptCount).HasColumnName("attempt_count").HasDefaultValue(0);
        outbox.HasIndex(x => new { x.TaxpayerId, x.DeduplicationKey }).IsUnique()
            .HasDatabaseName("ux_outbox_taxpayer_deduplication");
        outbox.HasIndex(x => new { x.ProcessedAtUtc, x.CreatedAtUtc })
            .HasDatabaseName("ix_outbox_pending");
    }
}
