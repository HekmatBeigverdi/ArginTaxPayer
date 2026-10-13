using ArginTaxPayer.Infrastructure.Persistence.Outbox;
using ArginTaxPayer.Infrastructure.Persistence.Idempotency;
using Microsoft.EntityFrameworkCore;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;

namespace ArginTaxPayer.Infrastructure.Persistence;

/// <summary>
/// Explicit migration-managed persistence context. Neither host applies
/// database schema changes automatically.
/// </summary>
public sealed class ArginTaxPayerDbContext(DbContextOptions<ArginTaxPayerDbContext> options)
    : DbContext(options)
{
    public DbSet<TaxpayerProfile> TaxpayerProfiles => Set<TaxpayerProfile>();
    public DbSet<TaxpayerDelegateGrant> TaxpayerDelegateGrants => Set<TaxpayerDelegateGrant>();
    public DbSet<FiscalMemory> FiscalMemories => Set<FiscalMemory>();
    public DbSet<FiscalCertificateMetadata> FiscalCertificateMetadata => Set<FiscalCertificateMetadata>();
    public DbSet<TaxpayerProfileChange> TaxpayerProfileChanges => Set<TaxpayerProfileChange>();
    public DbSet<IdentityUser> IdentityUsers => Set<IdentityUser>();
    public DbSet<IdentityUserRole> IdentityUserRoles => Set<IdentityUserRole>();
    public DbSet<IdentitySession> IdentitySessions => Set<IdentitySession>();
    public DbSet<IdentityMfaState> IdentityMfaStates => Set<IdentityMfaState>();
    public DbSet<IdentityRecoveryCode> IdentityRecoveryCodes => Set<IdentityRecoveryCode>();
    public DbSet<IdentityMfaChallenge> IdentityMfaChallenges => Set<IdentityMfaChallenge>();
    public DbSet<IdentityPhoneChallenge> IdentityPhoneChallenges => Set<IdentityPhoneChallenge>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<IdempotencyReservation> IdempotencyReservations => Set<IdempotencyReservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var grants = modelBuilder.Entity<TaxpayerDelegateGrant>();
        grants.ToTable("taxpayer_delegate_grants", t =>
        {
            t.HasCheckConstraint("ck_delegate_revision", "revision > 0");
            t.HasCheckConstraint("ck_delegate_revocation", "revoked_at_utc IS NULL OR (revoked_at_utc >= granted_at_utc AND revoked_by_level1_user_id IS NOT NULL AND length(trim(revocation_reason)) > 0)");
        });
        grants.HasKey(x => x.Id);
        grants.Property(x => x.Id).HasColumnName("id");
        grants.Property(x => x.TaxpayerId).HasColumnName("taxpayer_id");
        grants.Property(x => x.DelegateUserId).HasColumnName("delegate_user_id");
        grants.Property(x => x.GrantedByLevel1UserId).HasColumnName("granted_by_level1_user_id");
        grants.Property(x => x.GrantedAtUtc).HasColumnName("granted_at_utc").HasColumnType("timestamp with time zone");
        grants.Property(x => x.RevokedAtUtc).HasColumnName("revoked_at_utc").HasColumnType("timestamp with time zone");
        grants.Property(x => x.RevokedByLevel1UserId).HasColumnName("revoked_by_level1_user_id");
        grants.Property(x => x.RevocationReason).HasColumnName("revocation_reason").HasMaxLength(500);
        grants.Property(x => x.Revision).HasColumnName("revision").HasDefaultValue(1L).IsConcurrencyToken();
        grants.HasOne<TaxpayerProfile>().WithMany().HasForeignKey(x => x.TaxpayerId).OnDelete(DeleteBehavior.Restrict);
        grants.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.DelegateUserId).OnDelete(DeleteBehavior.Restrict);
        grants.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.GrantedByLevel1UserId).OnDelete(DeleteBehavior.Restrict);
        grants.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.RevokedByLevel1UserId).OnDelete(DeleteBehavior.Restrict);
        grants.HasIndex(x => new { x.TaxpayerId, x.DelegateUserId }).IsUnique()
            .HasFilter("revoked_at_utc IS NULL").HasDatabaseName("ux_taxpayer_delegate_active");
        grants.HasIndex(x => new { x.DelegateUserId, x.RevokedAtUtc }).HasDatabaseName("ix_delegate_current_scope");

        var certificates = modelBuilder.Entity<FiscalCertificateMetadata>();
        certificates.ToTable("fiscal_certificate_metadata", t =>
        {
            t.HasCheckConstraint("ck_fiscal_certificate_fingerprint", "fingerprint_sha256 ~ '^[0-9a-f]{64}$'");
            t.HasCheckConstraint("ck_fiscal_certificate_status", "status IN (1, 2, 3, 4)");
            t.HasCheckConstraint("ck_fiscal_certificate_origin", "origin IN (1, 2)");
            t.HasCheckConstraint("ck_fiscal_certificate_custody", "custody_mode = 1");
            t.HasCheckConstraint("ck_fiscal_certificate_validity", "not_before_utc IS NULL OR not_after_utc IS NULL OR not_before_utc < not_after_utc");
        });
        certificates.HasKey(x => x.Id);
        certificates.Property(x => x.Id).HasColumnName("id");
        certificates.Property(x => x.FiscalMemoryId).HasColumnName("fiscal_memory_id");
        certificates.Property(x => x.FingerprintSha256).HasColumnName("fingerprint_sha256").HasMaxLength(64).IsRequired();
        certificates.Property(x => x.Issuer).HasColumnName("issuer").HasMaxLength(256);
        certificates.Property(x => x.NotBeforeUtc).HasColumnName("not_before_utc").HasColumnType("timestamp with time zone");
        certificates.Property(x => x.NotAfterUtc).HasColumnName("not_after_utc").HasColumnType("timestamp with time zone");
        certificates.Property(x => x.Status).HasColumnName("status").HasConversion<int>();
        certificates.Property(x => x.Origin).HasColumnName("origin").HasConversion<int>();
        certificates.Property(x => x.CustodyMode).HasColumnName("custody_mode").HasConversion<int>();
        certificates.Property(x => x.CustodyReference).HasColumnName("custody_reference").HasMaxLength(256).IsRequired();
        certificates.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        certificates.Property(x => x.SupersededAtUtc).HasColumnName("superseded_at_utc").HasColumnType("timestamp with time zone");
        certificates.HasOne(x => x.FiscalMemory).WithMany().HasForeignKey(x => x.FiscalMemoryId).OnDelete(DeleteBehavior.Restrict);
        certificates.HasIndex(x => x.FiscalMemoryId).IsUnique().HasFilter("superseded_at_utc IS NULL")
            .HasDatabaseName("ux_fiscal_certificate_current_memory");
        certificates.HasIndex(x => new { x.FiscalMemoryId, x.CreatedAtUtc })
            .HasDatabaseName("ix_fiscal_certificate_history");

        var memories = modelBuilder.Entity<FiscalMemory>();
        memories.ToTable("fiscal_memories", table =>
        {
            table.HasCheckConstraint("ck_fiscal_memories_status", "status IN (1, 2, 3, 4)");
            table.HasCheckConstraint("ck_fiscal_memories_identifier", "length(trim(memory_identifier)) > 0");
            table.HasCheckConstraint("ck_fiscal_memories_revision", "revision > 0");
        });
        memories.HasKey(x => x.Id);
        memories.Property(x => x.Id).HasColumnName("id");
        memories.Property(x => x.TaxpayerId).HasColumnName("taxpayer_id").IsRequired();
        memories.Property(x => x.MemoryIdentifier).HasColumnName("memory_identifier").HasMaxLength(64).IsRequired();
        memories.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        memories.Property(x => x.Revision).HasColumnName("revision").HasDefaultValue(1L).IsConcurrencyToken();
        memories.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        memories.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");
        memories.HasOne(x => x.Taxpayer).WithMany().HasForeignKey(x => x.TaxpayerId)
            .OnDelete(DeleteBehavior.Restrict);
        memories.HasIndex(x => x.MemoryIdentifier).IsUnique()
            .HasDatabaseName("ux_fiscal_memories_identifier");
        memories.HasIndex(x => x.TaxpayerId).IsUnique()
            .HasFilter("status = 2").HasDatabaseName("ux_fiscal_memories_active_taxpayer");

        var changes = modelBuilder.Entity<TaxpayerProfileChange>();
        changes.ToTable("taxpayer_profile_changes");
        changes.HasKey(x => x.Id);
        changes.Property(x => x.Id).HasColumnName("id");
        changes.Property(x => x.TaxpayerId).HasColumnName("taxpayer_id");
        changes.Property(x => x.ActorUserId).HasColumnName("actor_user_id");
        changes.Property(x => x.FromRevision).HasColumnName("from_revision");
        changes.Property(x => x.ToRevision).HasColumnName("to_revision");
        changes.Property(x => x.PriorDisplayName).HasColumnName("prior_display_name").HasMaxLength(256).IsRequired();
        changes.Property(x => x.NewDisplayName).HasColumnName("new_display_name").HasMaxLength(256).IsRequired();
        changes.Property(x => x.PriorStatus).HasColumnName("prior_status").HasConversion<int>();
        changes.Property(x => x.NewStatus).HasColumnName("new_status").HasConversion<int>();
        changes.Property(x => x.ChangedAtUtc).HasColumnName("changed_at_utc").HasColumnType("timestamp with time zone");
        changes.HasOne<TaxpayerProfile>().WithMany().HasForeignKey(x => x.TaxpayerId).OnDelete(DeleteBehavior.Restrict);
        changes.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        changes.HasIndex(x => new { x.TaxpayerId, x.ToRevision }).IsUnique().HasDatabaseName("ux_taxpayer_profile_changes_revision");

        var taxpayers = modelBuilder.Entity<TaxpayerProfile>();
        taxpayers.ToTable("taxpayer_profiles", t =>
        {
            t.HasCheckConstraint("ck_taxpayer_profiles_kind", "kind IN (1, 2)");
            t.HasCheckConstraint("ck_taxpayer_profiles_status", "status IN (1, 2, 3)");
            t.HasCheckConstraint("ck_taxpayer_profiles_identifier", "length(trim(normalized_identifier)) > 0");
            t.HasCheckConstraint("ck_taxpayer_profiles_display_name", "length(trim(display_name)) > 0");
            t.HasCheckConstraint("ck_taxpayer_profiles_revision", "revision > 0");
        });
        taxpayers.HasKey(x => x.Id);
        taxpayers.Property(x => x.Id).HasColumnName("id");
        taxpayers.Property(x => x.Kind).HasColumnName("kind").HasConversion<int>().IsRequired();
        taxpayers.Property(x => x.NormalizedIdentifier).HasColumnName("normalized_identifier").HasMaxLength(32).IsRequired();
        taxpayers.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(256).IsRequired();
        taxpayers.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        taxpayers.Property(x => x.Revision).HasColumnName("revision").HasDefaultValue(1L).IsConcurrencyToken();
        taxpayers.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        taxpayers.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");
        taxpayers.HasIndex(x => new { x.Kind, x.NormalizedIdentifier })
            .IsUnique().HasDatabaseName("ux_taxpayer_profiles_kind_identifier");

        var users = modelBuilder.Entity<IdentityUser>();
        users.ToTable("identity_users");
        users.HasKey(x => x.Id);
        users.Property(x => x.Id).HasColumnName("id");
        users.Property(x => x.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(320);
        users.Property(x => x.NormalizedMobile).HasColumnName("normalized_mobile").HasMaxLength(13);
        users.Property(x => x.MobileVerifiedAtUtc).HasColumnName("mobile_verified_at_utc").HasColumnType("timestamp with time zone");
        users.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(512);
        users.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        users.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        users.Property(x => x.PasswordChangedAtUtc).HasColumnName("password_changed_at_utc").HasColumnType("timestamp with time zone");
        users.HasIndex(x => x.NormalizedEmail).IsUnique().HasDatabaseName("ux_identity_users_email");
        users.HasIndex(x => x.NormalizedMobile).IsUnique().HasDatabaseName("ux_identity_users_mobile");

        var roles = modelBuilder.Entity<IdentityUserRole>();
        roles.ToTable("identity_user_roles");
        roles.HasKey(x => new { x.UserId, x.Role });
        roles.Property(x => x.UserId).HasColumnName("user_id");
        roles.Property(x => x.Role).HasColumnName("role").HasMaxLength(32).IsRequired();
        roles.Property(x => x.AssignedAtUtc).HasColumnName("assigned_at_utc").HasColumnType("timestamp with time zone");
        roles.HasOne(x => x.User).WithMany(x => x.Roles).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        roles.HasIndex(x => x.Role).HasDatabaseName("ix_identity_user_roles_role");

        var sessions = modelBuilder.Entity<IdentitySession>();
        sessions.ToTable("identity_sessions");
        sessions.HasKey(x => x.Id);
        sessions.Property(x => x.Id).HasColumnName("id");
        sessions.Property(x => x.UserId).HasColumnName("user_id");
        sessions.Property(x => x.AccessTokenHash).HasColumnName("access_token_hash").HasMaxLength(64).IsRequired();
        sessions.Property(x => x.RefreshTokenHash).HasColumnName("refresh_token_hash").HasMaxLength(64).IsRequired();
        sessions.Property(x => x.PreviousRefreshTokenHash).HasColumnName("previous_refresh_token_hash").HasMaxLength(64);
        sessions.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        sessions.Property(x => x.AccessExpiresAtUtc).HasColumnName("access_expires_at_utc").HasColumnType("timestamp with time zone");
        sessions.Property(x => x.RefreshExpiresAtUtc).HasColumnName("refresh_expires_at_utc").HasColumnType("timestamp with time zone");
        sessions.Property(x => x.RevokedAtUtc).HasColumnName("revoked_at_utc").HasColumnType("timestamp with time zone");
        sessions.Property(x => x.RotationCount).HasColumnName("rotation_count").HasDefaultValue(0);
        sessions.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        sessions.HasIndex(x => x.AccessTokenHash).IsUnique().HasDatabaseName("ux_identity_sessions_access_hash");
        sessions.HasIndex(x => x.RefreshTokenHash).IsUnique().HasDatabaseName("ux_identity_sessions_refresh_hash");
        sessions.HasIndex(x => x.PreviousRefreshTokenHash).HasDatabaseName("ix_identity_sessions_previous_refresh_hash");
        sessions.HasIndex(x => new { x.UserId, x.RevokedAtUtc }).HasDatabaseName("ix_identity_sessions_user_revoked");

        var mfa = modelBuilder.Entity<IdentityMfaState>();
        mfa.ToTable("identity_mfa_states");
        mfa.HasKey(x => x.UserId);
        mfa.Property(x => x.UserId).HasColumnName("user_id");
        mfa.Property(x => x.EncryptedSecret).HasColumnName("encrypted_secret").HasColumnType("text").IsRequired();
        mfa.Property(x => x.EnabledAtUtc).HasColumnName("enabled_at_utc").HasColumnType("timestamp with time zone");
        mfa.Property(x => x.LastAcceptedTotpStep).HasColumnName("last_accepted_totp_step").HasDefaultValue(-1L);
        mfa.Property(x => x.RecoveryGeneration).HasColumnName("recovery_generation").HasDefaultValue(0);
        mfa.HasOne(x => x.User).WithOne().HasForeignKey<IdentityMfaState>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        var recovery = modelBuilder.Entity<IdentityRecoveryCode>();
        recovery.ToTable("identity_recovery_codes");
        recovery.HasKey(x => x.Id);
        recovery.Property(x => x.Id).HasColumnName("id");
        recovery.Property(x => x.UserId).HasColumnName("user_id");
        recovery.Property(x => x.CodeHash).HasColumnName("code_hash").HasMaxLength(64).IsRequired();
        recovery.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        recovery.Property(x => x.UsedAtUtc).HasColumnName("used_at_utc").HasColumnType("timestamp with time zone");
        recovery.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        recovery.HasIndex(x => new { x.UserId, x.CodeHash }).IsUnique().HasDatabaseName("ux_identity_recovery_user_hash");

        var challenge = modelBuilder.Entity<IdentityMfaChallenge>();
        challenge.ToTable("identity_mfa_challenges");
        challenge.HasKey(x => x.Id);
        challenge.Property(x => x.Id).HasColumnName("id");
        challenge.Property(x => x.UserId).HasColumnName("user_id");
        challenge.Property(x => x.ChallengeHash).HasColumnName("challenge_hash").HasMaxLength(64).IsRequired();
        challenge.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc").HasColumnType("timestamp with time zone");
        challenge.Property(x => x.ConsumedAtUtc).HasColumnName("consumed_at_utc").HasColumnType("timestamp with time zone");
        challenge.Property(x => x.AttemptCount).HasColumnName("attempt_count").HasDefaultValue(0);
        challenge.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        challenge.HasIndex(x => x.ChallengeHash).IsUnique().HasDatabaseName("ux_identity_challenge_hash");

        var phone = modelBuilder.Entity<IdentityPhoneChallenge>();
        phone.ToTable("identity_phone_challenges");
        phone.HasKey(x => x.Id);
        phone.Property(x => x.Id).HasColumnName("id");
        phone.Property(x => x.Mobile).HasColumnName("mobile").HasMaxLength(13).IsRequired();
        phone.Property(x => x.Purpose).HasColumnName("purpose").HasMaxLength(16).IsRequired();
        phone.Property(x => x.CodeDigest).HasColumnName("code_digest").HasMaxLength(64).IsRequired();
        phone.Property(x => x.UserId).HasColumnName("user_id");
        phone.Property(x => x.Attempts).HasColumnName("attempts");
        phone.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        phone.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc").HasColumnType("timestamp with time zone");
        phone.Property(x => x.ConsumedAtUtc).HasColumnName("consumed_at_utc").HasColumnType("timestamp with time zone");
        phone.Property(x => x.Delivered).HasColumnName("delivered");
        phone.HasIndex(x => new { x.Mobile, x.Purpose, x.CreatedAtUtc })
            .HasDatabaseName("ix_phone_challenge_cooldown");
        phone.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

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
