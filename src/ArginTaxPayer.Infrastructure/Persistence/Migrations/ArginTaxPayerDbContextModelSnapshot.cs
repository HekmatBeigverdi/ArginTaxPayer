using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
public sealed class ArginTaxPayerDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "9.0.4");

        modelBuilder.Entity("ArginTaxPayer.Infrastructure.Persistence.Taxpayers.TaxpayerProfileChange", b =>
        {
            b.Property<Guid>("Id").HasColumnName("id").HasColumnType("uuid");
            b.Property<Guid>("TaxpayerId").HasColumnName("taxpayer_id").HasColumnType("uuid");
            b.Property<Guid>("ActorUserId").HasColumnName("actor_user_id").HasColumnType("uuid");
            b.Property<long>("FromRevision").HasColumnName("from_revision").HasColumnType("bigint");
            b.Property<long>("ToRevision").HasColumnName("to_revision").HasColumnType("bigint");
            b.Property<string>("PriorDisplayName").IsRequired().HasMaxLength(256).HasColumnName("prior_display_name").HasColumnType("character varying(256)");
            b.Property<string>("NewDisplayName").IsRequired().HasMaxLength(256).HasColumnName("new_display_name").HasColumnType("character varying(256)");
            b.Property<int>("PriorStatus").HasColumnName("prior_status").HasColumnType("integer");
            b.Property<int>("NewStatus").HasColumnName("new_status").HasColumnType("integer");
            b.Property<DateTime>("ChangedAtUtc").HasColumnName("changed_at_utc").HasColumnType("timestamp with time zone");
            b.HasKey("Id");
            b.HasIndex("TaxpayerId", "ToRevision").IsUnique().HasDatabaseName("ux_taxpayer_profile_changes_revision");
            b.HasIndex("ActorUserId");
            b.ToTable("taxpayer_profile_changes");
        });
        modelBuilder.Entity("ArginTaxPayer.Infrastructure.Persistence.Taxpayers.TaxpayerProfileChange", b =>
        {
            b.HasOne("ArginTaxPayer.Infrastructure.Persistence.Taxpayers.TaxpayerProfile", null)
                .WithMany().HasForeignKey("TaxpayerId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            b.HasOne("ArginTaxPayer.Infrastructure.Persistence.Identity.IdentityUser", null)
                .WithMany().HasForeignKey("ActorUserId").OnDelete(DeleteBehavior.Restrict).IsRequired();
        });

        modelBuilder.Entity("ArginTaxPayer.Infrastructure.Persistence.Taxpayers.TaxpayerProfile", b =>
        {
            b.Property<Guid>("Id").HasColumnName("id").HasColumnType("uuid");
            b.Property<int>("Kind").HasColumnName("kind").HasColumnType("integer");
            b.Property<string>("NormalizedIdentifier").IsRequired().HasMaxLength(32).HasColumnName("normalized_identifier").HasColumnType("character varying(32)");
            b.Property<string>("DisplayName").IsRequired().HasMaxLength(256).HasColumnName("display_name").HasColumnType("character varying(256)");
            b.Property<int>("Status").HasColumnName("status").HasColumnType("integer");
            b.Property<long>("Revision").HasColumnName("revision").HasColumnType("bigint").HasDefaultValue(1L).IsConcurrencyToken();
            b.Property<DateTime>("CreatedAtUtc").HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
            b.Property<DateTime>("UpdatedAtUtc").HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");
            b.HasKey("Id");
            b.HasIndex("Kind", "NormalizedIdentifier").IsUnique().HasDatabaseName("ux_taxpayer_profiles_kind_identifier");
            b.ToTable("taxpayer_profiles", t =>
            {
                t.HasCheckConstraint("ck_taxpayer_profiles_kind", "kind IN (1, 2)");
                t.HasCheckConstraint("ck_taxpayer_profiles_status", "status IN (1, 2, 3)");
                t.HasCheckConstraint("ck_taxpayer_profiles_identifier", "length(trim(normalized_identifier)) > 0");
                t.HasCheckConstraint("ck_taxpayer_profiles_display_name", "length(trim(display_name)) > 0");
                t.HasCheckConstraint("ck_taxpayer_profiles_revision", "revision > 0");
            });
        });

        modelBuilder.Entity("ArginTaxPayer.Infrastructure.Persistence.Identity.IdentityUser", b =>
        {
            b.Property<Guid>("Id").HasColumnName("id").HasColumnType("uuid");
            b.Property<string>("NormalizedEmail").HasMaxLength(320).HasColumnName("normalized_email").HasColumnType("character varying(320)");
            b.Property<string>("PasswordHash").HasMaxLength(512).HasColumnName("password_hash").HasColumnType("character varying(512)");
            b.Property<string>("NormalizedMobile").HasMaxLength(13).HasColumnName("normalized_mobile").HasColumnType("character varying(13)");
            b.Property<DateTime?>("MobileVerifiedAtUtc").HasColumnName("mobile_verified_at_utc").HasColumnType("timestamp with time zone");
            b.Property<bool>("IsActive").HasColumnName("is_active").HasColumnType("boolean");
            b.Property<DateTime>("CreatedAtUtc").HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
            b.Property<DateTime?>("PasswordChangedAtUtc").HasColumnName("password_changed_at_utc").HasColumnType("timestamp with time zone");
            b.HasKey("Id");
            b.HasIndex("NormalizedEmail").IsUnique().HasDatabaseName("ux_identity_users_email");
            b.HasIndex("NormalizedMobile").IsUnique().HasDatabaseName("ux_identity_users_mobile");
            b.ToTable("identity_users");
        });
        modelBuilder.Entity("ArginTaxPayer.Infrastructure.Persistence.Identity.IdentityPhoneChallenge", b =>
        {
            b.Property<Guid>("Id").HasColumnName("id").HasColumnType("uuid");
            b.Property<string>("Mobile").IsRequired().HasMaxLength(13).HasColumnName("mobile").HasColumnType("character varying(13)");
            b.Property<string>("Purpose").IsRequired().HasMaxLength(16).HasColumnName("purpose").HasColumnType("character varying(16)");
            b.Property<string>("CodeDigest").IsRequired().HasMaxLength(64).HasColumnName("code_digest").HasColumnType("character varying(64)");
            b.Property<Guid?>("UserId").HasColumnName("user_id").HasColumnType("uuid");
            b.Property<int>("Attempts").HasColumnName("attempts").HasColumnType("integer");
            b.Property<DateTime>("CreatedAtUtc").HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
            b.Property<DateTime>("ExpiresAtUtc").HasColumnName("expires_at_utc").HasColumnType("timestamp with time zone");
            b.Property<DateTime?>("ConsumedAtUtc").HasColumnName("consumed_at_utc").HasColumnType("timestamp with time zone");
            b.Property<bool>("Delivered").HasColumnName("delivered").HasColumnType("boolean");
            b.HasKey("Id");
            b.HasIndex("Mobile", "Purpose", "CreatedAtUtc").HasDatabaseName("ix_phone_challenge_cooldown");
            b.HasIndex("UserId");
            b.ToTable("identity_phone_challenges");
        });
        modelBuilder.Entity("ArginTaxPayer.Infrastructure.Persistence.Identity.IdentityPhoneChallenge", b =>
        {
            b.HasOne("ArginTaxPayer.Infrastructure.Persistence.Identity.IdentityUser", null)
                .WithMany().HasForeignKey("UserId").OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity("ArginTaxPayer.Infrastructure.Persistence.Identity.IdentityUserRole", b =>
        {
            b.Property<Guid>("UserId").HasColumnName("user_id").HasColumnType("uuid");
            b.Property<string>("Role").IsRequired().HasMaxLength(32).HasColumnName("role").HasColumnType("character varying(32)");
            b.Property<DateTime>("AssignedAtUtc").HasColumnName("assigned_at_utc").HasColumnType("timestamp with time zone");
            b.HasKey("UserId", "Role");
            b.HasIndex("Role").HasDatabaseName("ix_identity_user_roles_role");
            b.ToTable("identity_user_roles");
        });
        modelBuilder.Entity("ArginTaxPayer.Infrastructure.Persistence.Identity.IdentityUserRole", b =>
        {
            b.HasOne("ArginTaxPayer.Infrastructure.Persistence.Identity.IdentityUser", "User")
                .WithMany("Roles").HasForeignKey("UserId").OnDelete(DeleteBehavior.Cascade).IsRequired();
            b.Navigation("User");
        });
        modelBuilder.Entity("ArginTaxPayer.Infrastructure.Persistence.Identity.IdentityUser", b => b.Navigation("Roles"));

        modelBuilder.Entity("ArginTaxPayer.Infrastructure.Persistence.Identity.IdentitySession", b =>
        {
            b.Property<Guid>("Id").HasColumnName("id").HasColumnType("uuid");
            b.Property<Guid>("UserId").HasColumnName("user_id").HasColumnType("uuid");
            b.Property<string>("AccessTokenHash").IsRequired().HasMaxLength(64).HasColumnName("access_token_hash").HasColumnType("character varying(64)");
            b.Property<string>("RefreshTokenHash").IsRequired().HasMaxLength(64).HasColumnName("refresh_token_hash").HasColumnType("character varying(64)");
            b.Property<string>("PreviousRefreshTokenHash").HasMaxLength(64).HasColumnName("previous_refresh_token_hash").HasColumnType("character varying(64)");
            b.Property<DateTime>("CreatedAtUtc").HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
            b.Property<DateTime>("AccessExpiresAtUtc").HasColumnName("access_expires_at_utc").HasColumnType("timestamp with time zone");
            b.Property<DateTime>("RefreshExpiresAtUtc").HasColumnName("refresh_expires_at_utc").HasColumnType("timestamp with time zone");
            b.Property<DateTime?>("RevokedAtUtc").HasColumnName("revoked_at_utc").HasColumnType("timestamp with time zone");
            b.Property<int>("RotationCount").HasColumnName("rotation_count").HasColumnType("integer").HasDefaultValue(0);
            b.HasKey("Id");
            b.HasIndex("AccessTokenHash").IsUnique().HasDatabaseName("ux_identity_sessions_access_hash");
            b.HasIndex("RefreshTokenHash").IsUnique().HasDatabaseName("ux_identity_sessions_refresh_hash");
            b.HasIndex("PreviousRefreshTokenHash").HasDatabaseName("ix_identity_sessions_previous_refresh_hash");
            b.HasIndex("UserId", "RevokedAtUtc").HasDatabaseName("ix_identity_sessions_user_revoked");
            b.ToTable("identity_sessions");
        });
        modelBuilder.Entity("ArginTaxPayer.Infrastructure.Persistence.Identity.IdentitySession", b =>
        {
            b.HasOne("ArginTaxPayer.Infrastructure.Persistence.Identity.IdentityUser", "User")
                .WithMany().HasForeignKey("UserId").OnDelete(DeleteBehavior.Cascade).IsRequired();
            b.Navigation("User");
        });

        modelBuilder.Entity("ArginTaxPayer.Infrastructure.Persistence.Identity.IdentityMfaState", b =>
        {
            b.Property<Guid>("UserId").HasColumnName("user_id").HasColumnType("uuid");
            b.Property<string>("EncryptedSecret").IsRequired().HasColumnName("encrypted_secret").HasColumnType("text");
            b.Property<DateTime?>("EnabledAtUtc").HasColumnName("enabled_at_utc").HasColumnType("timestamp with time zone");
            b.Property<long>("LastAcceptedTotpStep").HasColumnName("last_accepted_totp_step").HasColumnType("bigint").HasDefaultValue(-1L);
            b.Property<int>("RecoveryGeneration").HasColumnName("recovery_generation").HasColumnType("integer").HasDefaultValue(0);
            b.HasKey("UserId");
            b.ToTable("identity_mfa_states");
        });
        modelBuilder.Entity("ArginTaxPayer.Infrastructure.Persistence.Identity.IdentityRecoveryCode", b =>
        {
            b.Property<Guid>("Id").HasColumnName("id").HasColumnType("uuid");
            b.Property<Guid>("UserId").HasColumnName("user_id").HasColumnType("uuid");
            b.Property<string>("CodeHash").IsRequired().HasMaxLength(64).HasColumnName("code_hash").HasColumnType("character varying(64)");
            b.Property<DateTime>("CreatedAtUtc").HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
            b.Property<DateTime?>("UsedAtUtc").HasColumnName("used_at_utc").HasColumnType("timestamp with time zone");
            b.HasKey("Id");
            b.HasIndex("UserId", "CodeHash").IsUnique().HasDatabaseName("ux_identity_recovery_user_hash");
            b.ToTable("identity_recovery_codes");
        });
        modelBuilder.Entity("ArginTaxPayer.Infrastructure.Persistence.Identity.IdentityMfaChallenge", b =>
        {
            b.Property<Guid>("Id").HasColumnName("id").HasColumnType("uuid");
            b.Property<Guid>("UserId").HasColumnName("user_id").HasColumnType("uuid");
            b.Property<string>("ChallengeHash").IsRequired().HasMaxLength(64).HasColumnName("challenge_hash").HasColumnType("character varying(64)");
            b.Property<DateTime>("ExpiresAtUtc").HasColumnName("expires_at_utc").HasColumnType("timestamp with time zone");
            b.Property<DateTime?>("ConsumedAtUtc").HasColumnName("consumed_at_utc").HasColumnType("timestamp with time zone");
            b.Property<int>("AttemptCount").HasColumnName("attempt_count").HasColumnType("integer").HasDefaultValue(0);
            b.HasKey("Id");
            b.HasIndex("ChallengeHash").IsUnique().HasDatabaseName("ux_identity_challenge_hash");
            b.ToTable("identity_mfa_challenges");
        });
        modelBuilder.Entity("ArginTaxPayer.Infrastructure.Persistence.Identity.IdentityMfaState", b =>
        {
            b.HasOne("ArginTaxPayer.Infrastructure.Persistence.Identity.IdentityUser", "User").WithOne()
                .HasForeignKey("ArginTaxPayer.Infrastructure.Persistence.Identity.IdentityMfaState", "UserId")
                .OnDelete(DeleteBehavior.Cascade).IsRequired();
            b.Navigation("User");
        });
        modelBuilder.Entity("ArginTaxPayer.Infrastructure.Persistence.Identity.IdentityRecoveryCode", b =>
        {
            b.HasOne("ArginTaxPayer.Infrastructure.Persistence.Identity.IdentityUser", "User").WithMany()
                .HasForeignKey("UserId").OnDelete(DeleteBehavior.Cascade).IsRequired();
            b.Navigation("User");
        });
        modelBuilder.Entity("ArginTaxPayer.Infrastructure.Persistence.Identity.IdentityMfaChallenge", b =>
        {
            b.HasOne("ArginTaxPayer.Infrastructure.Persistence.Identity.IdentityUser", "User").WithMany()
                .HasForeignKey("UserId").OnDelete(DeleteBehavior.Cascade).IsRequired();
            b.Navigation("User");
        });

        modelBuilder.Entity("ArginTaxPayer.Infrastructure.Persistence.Idempotency.IdempotencyReservation", b =>
        {
            b.Property<Guid>("Id").HasColumnName("id").HasColumnType("uuid");
            b.Property<Guid>("TaxpayerId").HasColumnName("taxpayer_id").HasColumnType("uuid");
            b.Property<string>("Operation").IsRequired().HasMaxLength(100).HasColumnName("operation").HasColumnType("character varying(100)");
            b.Property<string>("Key").IsRequired().HasMaxLength(200).HasColumnName("idempotency_key").HasColumnType("character varying(200)");
            b.Property<string>("RequestFingerprint").IsRequired().HasMaxLength(64).HasColumnName("request_fingerprint").HasColumnType("character varying(64)");
            b.Property<DateTime>("CreatedAtUtc").HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
            b.HasKey("Id");
            b.HasIndex("TaxpayerId", "Operation", "Key").IsUnique().HasDatabaseName("ux_idempotency_taxpayer_operation_key");
            b.ToTable("idempotency_reservations");
        });

        modelBuilder.Entity("ArginTaxPayer.Infrastructure.Persistence.Outbox.OutboxMessage", b =>
        {
            b.Property<Guid>("Id").HasColumnName("id").HasColumnType("uuid");
            b.Property<Guid>("TaxpayerId").HasColumnName("taxpayer_id").HasColumnType("uuid");
            b.Property<string>("EventType").IsRequired().HasMaxLength(200).HasColumnName("event_type").HasColumnType("character varying(200)");
            b.Property<string>("PayloadJson").IsRequired().HasColumnName("payload_json").HasColumnType("text");
            b.Property<string>("DeduplicationKey").IsRequired().HasMaxLength(200).HasColumnName("deduplication_key").HasColumnType("character varying(200)");
            b.Property<DateTime>("CreatedAtUtc").HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
            b.Property<DateTime?>("ProcessedAtUtc").HasColumnName("processed_at_utc").HasColumnType("timestamp with time zone");
            b.Property<int>("AttemptCount").HasColumnName("attempt_count").HasColumnType("integer").HasDefaultValue(0);
            b.HasKey("Id");
            b.HasIndex("TaxpayerId", "DeduplicationKey").IsUnique().HasDatabaseName("ux_outbox_taxpayer_deduplication");
            b.HasIndex("ProcessedAtUtc", "CreatedAtUtc").HasDatabaseName("ix_outbox_pending");
            b.ToTable("outbox_messages");
        });
    }
}
