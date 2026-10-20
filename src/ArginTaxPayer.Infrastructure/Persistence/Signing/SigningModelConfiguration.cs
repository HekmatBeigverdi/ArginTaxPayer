using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Signing;

/// <summary>Phase09 metadata mapping. Database-only scope guards preserve nullable legacy owners.</summary>
internal static class SigningModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SigningOperation>(b =>
        {
            b.Property<Guid>("Id").HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            b.Property<Guid>("CustomerUserId").HasColumnName("customer_user_id").HasColumnType("uuid");
            b.Property<Guid>("TaxpayerId").HasColumnName("taxpayer_id").HasColumnType("uuid");
            b.Property<Guid>("FiscalMemoryId").HasColumnName("fiscal_memory_id").HasColumnType("uuid");
            b.Property<Guid>("OperationId").HasColumnName("operation_id").HasColumnType("uuid");
            b.Property<Guid>("ActorUserId").HasColumnName("actor_user_id").HasColumnType("uuid");
            b.Property<short>("OperationKind").HasColumnName("operation_kind").HasColumnType("smallint");
            b.Property<string>("ProfileId").HasColumnName("profile_id").HasColumnType("character varying(96)").IsRequired().HasMaxLength(96);
            b.Property<string>("MetadataDigestSha256").HasColumnName("metadata_digest_sha256").HasColumnType("character varying(64)").IsRequired().HasMaxLength(64);
            b.Property<short>("State").HasColumnName("state").HasColumnType("smallint");
            b.Property<string>("SafeErrorCode").HasColumnName("safe_error_code").HasColumnType("character varying(64)").HasMaxLength(64);
            b.Property<long>("Revision").HasColumnName("revision").HasColumnType("bigint").IsConcurrencyToken().HasDefaultValue(1L);
            b.Property<DateTime>("CreatedAtUtc").HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
            b.Property<DateTime>("UpdatedAtUtc").HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");
            b.HasKey("Id").HasName("signing_operations_pkey");
            b.HasAlternateKey("CustomerUserId", "TaxpayerId", "FiscalMemoryId", "OperationId").HasName("ux_signing_op_idempotency");
            b.HasAlternateKey("Id", "CustomerUserId", "TaxpayerId", "FiscalMemoryId").HasName("ux_signing_op_scope");
            b.HasIndex(new[] { "State", "UpdatedAtUtc", "Id" }, "ix_signing_op_recovery").HasDatabaseName("ix_signing_op_recovery").HasFilter("state IN (1,2,5)");
            b.HasIndex(new[] { "TaxpayerId" }, "ix_p09_op_fk1").HasDatabaseName("ix_p09_op_fk1");
            b.HasIndex(new[] { "FiscalMemoryId" }, "ix_p09_op_fk2").HasDatabaseName("ix_p09_op_fk2");
            b.HasIndex(new[] { "ActorUserId" }, "ix_p09_op_fk3").HasDatabaseName("ix_p09_op_fk3");
            b.ToTable("signing_operations", t =>
            {
                t.HasCheckConstraint("ck_p09_op_1", "operation_kind BETWEEN 1 AND 4");
                t.HasCheckConstraint("ck_p09_op_2", "metadata_digest_sha256 ~ '^[0-9a-f]{64}$'");
                t.HasCheckConstraint("ck_p09_op_3", "state BETWEEN 1 AND 5");
                t.HasCheckConstraint("ck_p09_op_4", "revision > 0");
                t.HasCheckConstraint("ck_p09_op_owner", "operation_kind NOT IN (1,2) OR actor_user_id = customer_user_id");
                t.HasCheckConstraint("ck_signing_op_time", "updated_at_utc >= created_at_utc");
            });
        });

        modelBuilder.Entity<SigningConsent>(b =>
        {
            b.Property<Guid>("Id").HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            b.Property<Guid>("OperationRecordId").HasColumnName("operation_record_id").HasColumnType("uuid");
            b.Property<Guid>("CustomerUserId").HasColumnName("customer_user_id").HasColumnType("uuid");
            b.Property<Guid>("TaxpayerId").HasColumnName("taxpayer_id").HasColumnType("uuid");
            b.Property<Guid>("FiscalMemoryId").HasColumnName("fiscal_memory_id").HasColumnType("uuid");
            b.Property<Guid>("ActorUserId").HasColumnName("actor_user_id").HasColumnType("uuid");
            b.Property<string>("ConsentVersion").HasColumnName("consent_version").HasColumnType("character varying(64)").IsRequired().HasMaxLength(64);
            b.Property<DateTime>("ReauthenticatedAtUtc").HasColumnName("reauthenticated_at_utc").HasColumnType("timestamp with time zone");
            b.Property<DateTime>("ConsentedAtUtc").HasColumnName("consented_at_utc").HasColumnType("timestamp with time zone");
            b.HasKey("Id").HasName("signing_consents_pkey");
            b.HasAlternateKey("OperationRecordId").HasName("ux_signing_consent_operation");
            b.HasIndex(new[] { "ActorUserId" }, "ix_p09_consent_fk1").HasDatabaseName("ix_p09_consent_fk1");
            b.HasIndex(new[] { "OperationRecordId", "CustomerUserId", "TaxpayerId", "FiscalMemoryId" }, "ix_p09_consent_fk2").HasDatabaseName("ix_p09_consent_fk2");
            b.ToTable("signing_consents", t =>
            {
                t.HasCheckConstraint("ck_p09_consent_1", "length(trim(consent_version)) > 0");
                t.HasCheckConstraint("ck_signing_consent_owner", "actor_user_id = customer_user_id");
                t.HasCheckConstraint("ck_signing_consent_time", "consented_at_utc >= reauthenticated_at_utc AND consented_at_utc <= reauthenticated_at_utc + interval '5 minutes'");
            });
        });

        modelBuilder.Entity<SigningKeyVersion>(b =>
        {
            b.Property<Guid>("Id").HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            b.Property<Guid>("CustomerUserId").HasColumnName("customer_user_id").HasColumnType("uuid");
            b.Property<Guid>("TaxpayerId").HasColumnName("taxpayer_id").HasColumnType("uuid");
            b.Property<Guid>("FiscalMemoryId").HasColumnName("fiscal_memory_id").HasColumnType("uuid");
            b.Property<Guid>("OriginOperationRecordId").HasColumnName("origin_operation_record_id").HasColumnType("uuid");
            b.Property<short>("Origin").HasColumnName("origin").HasColumnType("smallint");
            b.Property<long>("VersionNumber").HasColumnName("version_number").HasColumnType("bigint");
            b.Property<string>("ProfileId").HasColumnName("profile_id").HasColumnType("character varying(96)").IsRequired().HasMaxLength(96);
            b.Property<string>("PublicFingerprintSha256").HasColumnName("public_fingerprint_sha256").HasColumnType("character varying(64)").HasMaxLength(64);
            b.Property<byte[]>("PublicSpkiDer").HasColumnName("public_spki_der").HasColumnType("bytea");
            b.Property<string>("CustodyReference").HasColumnName("custody_reference").HasColumnType("character varying(256)").HasMaxLength(256);
            b.Property<string>("ProtectionKeyVersion").HasColumnName("protection_key_version").HasColumnType("character varying(64)").HasMaxLength(64);
            b.Property<short>("LifecycleState").HasColumnName("lifecycle_state").HasColumnType("smallint");
            b.Property<short>("PortalState").HasColumnName("portal_state").HasColumnType("smallint").HasDefaultValue((short)1);
            b.Property<short>("PublicMatch").HasColumnName("public_match").HasColumnType("smallint").HasDefaultValue((short)1);
            b.Property<long>("Revision").HasColumnName("revision").HasColumnType("bigint").IsConcurrencyToken().HasDefaultValue(1L);
            b.Property<DateTime>("CreatedAtUtc").HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
            b.Property<DateTime>("UpdatedAtUtc").HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");
            b.HasKey("Id").HasName("signing_key_versions_pkey");
            b.HasAlternateKey("OriginOperationRecordId").HasName("ux_signing_key_origin");
            b.HasAlternateKey("FiscalMemoryId", "VersionNumber").HasName("ux_signing_key_version");
            b.HasAlternateKey("Id", "CustomerUserId", "TaxpayerId", "FiscalMemoryId").HasName("ux_signing_key_scope");
            b.HasIndex(new[] { "CustodyReference" }, "ux_signing_key_custody").HasDatabaseName("ux_signing_key_custody").IsUnique().HasFilter("custody_reference IS NOT NULL");
            b.HasIndex(new[] { "CustomerUserId", "TaxpayerId", "CreatedAtUtc", "Id" }, "ix_signing_key_customer_history").HasDatabaseName("ix_signing_key_customer_history");
            b.HasIndex(new[] { "OriginOperationRecordId", "CustomerUserId", "TaxpayerId", "FiscalMemoryId" }, "ix_p09_key_fk1").HasDatabaseName("ix_p09_key_fk1");
            b.ToTable("signing_key_versions", t =>
            {
                t.HasCheckConstraint("ck_p09_key_1", "origin IN (1,2)");
                t.HasCheckConstraint("ck_p09_key_2", "version_number > 0");
                t.HasCheckConstraint("ck_p09_key_3", "public_fingerprint_sha256 ~ '^[0-9a-f]{64}$'");
                t.HasCheckConstraint("ck_p09_key_4", "octet_length(public_spki_der) BETWEEN 1 AND 4096");
                t.HasCheckConstraint("ck_p09_key_5", "custody_reference ~ '^vault://[A-Za-z0-9/_-]{1,248}$'");
                t.HasCheckConstraint("ck_p09_key_6", "lifecycle_state BETWEEN 1 AND 5");
                t.HasCheckConstraint("ck_p09_key_7", "portal_state IN (1,2)");
                t.HasCheckConstraint("ck_p09_key_8", "public_match IN (1,2,3)");
                t.HasCheckConstraint("ck_p09_key_9", "revision > 0");
                t.HasCheckConstraint("ck_signing_key_time", "updated_at_utc >= created_at_utc");
                t.HasCheckConstraint("ck_signing_key_sealed", "lifecycle_state = 1 OR (public_fingerprint_sha256 IS NOT NULL AND public_spki_der IS NOT NULL AND custody_reference IS NOT NULL AND protection_key_version IS NOT NULL)");
                t.HasCheckConstraint("ck_signing_key_ready_match", "lifecycle_state <> 2 OR public_match <> 3");
            });
        });

        modelBuilder.Entity<SigningKeySelection>(b =>
        {
            b.Property<Guid>("FiscalMemoryId").HasColumnName("fiscal_memory_id").HasColumnType("uuid").ValueGeneratedNever();
            b.Property<Guid>("CustomerUserId").HasColumnName("customer_user_id").HasColumnType("uuid");
            b.Property<Guid>("TaxpayerId").HasColumnName("taxpayer_id").HasColumnType("uuid");
            b.Property<Guid?>("KeyVersionId").HasColumnName("key_version_id").HasColumnType("uuid");
            b.Property<long>("Revision").HasColumnName("revision").HasColumnType("bigint").IsConcurrencyToken().HasDefaultValue(1L);
            b.Property<DateTime>("SelectedAtUtc").HasColumnName("selected_at_utc").HasColumnType("timestamp with time zone");
            b.Property<Guid>("SelectedByUserId").HasColumnName("selected_by_user_id").HasColumnType("uuid");
            b.HasKey("FiscalMemoryId").HasName("signing_key_selections_pkey");
            b.HasIndex(new[] { "SelectedByUserId" }, "ix_p09_selection_fk1").HasDatabaseName("ix_p09_selection_fk1");
            b.HasIndex(new[] { "KeyVersionId", "CustomerUserId", "TaxpayerId", "FiscalMemoryId" }, "ix_p09_selection_fk2").HasDatabaseName("ix_p09_selection_fk2");
            b.ToTable("signing_key_selections", t =>
            {
                t.HasCheckConstraint("ck_p09_selection_1", "revision > 0");
            });
        });

        modelBuilder.Entity<SigningChangeRequest>(b =>
        {
            b.Property<Guid>("Id").HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            b.Property<Guid>("CustomerUserId").HasColumnName("customer_user_id").HasColumnType("uuid");
            b.Property<Guid>("TaxpayerId").HasColumnName("taxpayer_id").HasColumnType("uuid");
            b.Property<Guid>("FiscalMemoryId").HasColumnName("fiscal_memory_id").HasColumnType("uuid");
            b.Property<Guid>("CurrentKeyVersionId").HasColumnName("current_key_version_id").HasColumnType("uuid");
            b.Property<long>("ExpectedKeyRevision").HasColumnName("expected_key_revision").HasColumnType("bigint");
            b.Property<short>("Kind").HasColumnName("kind").HasColumnType("smallint");
            b.Property<short>("State").HasColumnName("state").HasColumnType("smallint");
            b.Property<string>("ReasonCode").HasColumnName("reason_code").HasColumnType("character varying(64)").IsRequired().HasMaxLength(64);
            b.Property<string>("Explanation").HasColumnName("explanation").HasColumnType("character varying(1000)").IsRequired().HasMaxLength(1000);
            b.Property<Guid>("RequestedByUserId").HasColumnName("requested_by_user_id").HasColumnType("uuid");
            b.Property<Guid?>("ReviewedByUserId").HasColumnName("reviewed_by_user_id").HasColumnType("uuid");
            b.Property<string>("DecisionReason").HasColumnName("decision_reason").HasColumnType("character varying(1000)").HasMaxLength(1000);
            b.Property<DateTime>("RequestedAtUtc").HasColumnName("requested_at_utc").HasColumnType("timestamp with time zone");
            b.Property<DateTime?>("ReviewedAtUtc").HasColumnName("reviewed_at_utc").HasColumnType("timestamp with time zone");
            b.Property<DateTime?>("ExpiresAtUtc").HasColumnName("expires_at_utc").HasColumnType("timestamp with time zone");
            b.Property<DateTime?>("ConsumedAtUtc").HasColumnName("consumed_at_utc").HasColumnType("timestamp with time zone");
            b.Property<Guid?>("ConsumedByOperationRecordId").HasColumnName("consumed_by_operation_record_id").HasColumnType("uuid");
            b.Property<long>("Revision").HasColumnName("revision").HasColumnType("bigint").IsConcurrencyToken().HasDefaultValue(1L);
            b.HasKey("Id").HasName("signing_change_requests_pkey");
            b.HasIndex(new[] { "CustomerUserId", "TaxpayerId", "State", "RequestedAtUtc" }, "ix_signing_review_scope_state").HasDatabaseName("ix_signing_review_scope_state");
            b.HasIndex(new[] { "ConsumedByOperationRecordId" }, "ux_p09_review_consumption").HasDatabaseName("ux_p09_review_consumption").IsUnique().HasFilter("consumed_by_operation_record_id IS NOT NULL");
            b.HasIndex(new[] { "RequestedByUserId" }, "ix_p09_review_fk1").HasDatabaseName("ix_p09_review_fk1");
            b.HasIndex(new[] { "ReviewedByUserId" }, "ix_p09_review_fk2").HasDatabaseName("ix_p09_review_fk2");
            b.HasIndex(new[] { "CurrentKeyVersionId", "CustomerUserId", "TaxpayerId", "FiscalMemoryId" }, "ix_p09_review_fk3").HasDatabaseName("ix_p09_review_fk3");
            b.HasIndex(new[] { "ConsumedByOperationRecordId", "CustomerUserId", "TaxpayerId", "FiscalMemoryId" }, "ix_p09_review_fk4").HasDatabaseName("ix_p09_review_fk4");
            b.ToTable("signing_change_requests", t =>
            {
                t.HasCheckConstraint("ck_p09_review_1", "expected_key_revision > 0");
                t.HasCheckConstraint("ck_p09_review_2", "kind BETWEEN 1 AND 3");
                t.HasCheckConstraint("ck_p09_review_3", "state BETWEEN 1 AND 5");
                t.HasCheckConstraint("ck_p09_review_4", "length(trim(explanation)) > 0");
                t.HasCheckConstraint("ck_p09_review_5", "revision > 0");
                t.HasCheckConstraint("ck_signing_review_distinct", "reviewed_by_user_id IS NULL OR reviewed_by_user_id <> requested_by_user_id");
                t.HasCheckConstraint("ck_signing_review_decision", "(state = 1 AND reviewed_by_user_id IS NULL AND reviewed_at_utc IS NULL AND expires_at_utc IS NULL) OR (state IN (2,3,4,5) AND reviewed_by_user_id IS NOT NULL AND reviewed_at_utc IS NOT NULL AND reviewed_at_utc >= requested_at_utc AND decision_reason IS NOT NULL AND length(trim(decision_reason)) > 0)");
                t.HasCheckConstraint("ck_signing_review_expiry", "(state = 3 AND expires_at_utc IS NULL) OR state = 1 OR (state IN (2,4,5) AND expires_at_utc IS NOT NULL AND expires_at_utc = reviewed_at_utc + interval '24 hours')");
                t.HasCheckConstraint("ck_signing_review_consumption", "(state = 4 AND consumed_at_utc IS NOT NULL AND consumed_by_operation_record_id IS NOT NULL AND consumed_at_utc >= reviewed_at_utc AND consumed_at_utc <= expires_at_utc) OR (state <> 4 AND consumed_at_utc IS NULL AND consumed_by_operation_record_id IS NULL)");
            });
        });

        modelBuilder.Entity<SigningAuditEvent>(b =>
        {
            b.Property<Guid>("Id").HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            b.Property<Guid>("CustomerUserId").HasColumnName("customer_user_id").HasColumnType("uuid");
            b.Property<Guid>("TaxpayerId").HasColumnName("taxpayer_id").HasColumnType("uuid");
            b.Property<Guid>("FiscalMemoryId").HasColumnName("fiscal_memory_id").HasColumnType("uuid");
            b.Property<Guid>("OperationRecordId").HasColumnName("operation_record_id").HasColumnType("uuid");
            b.Property<Guid>("ActorUserId").HasColumnName("actor_user_id").HasColumnType("uuid");
            b.Property<short>("SubjectKind").HasColumnName("subject_kind").HasColumnType("smallint");
            b.Property<Guid>("SubjectId").HasColumnName("subject_id").HasColumnType("uuid");
            b.Property<string>("ActionCode").HasColumnName("action_code").HasColumnType("character varying(64)").IsRequired().HasMaxLength(64);
            b.Property<string>("OutcomeCode").HasColumnName("outcome_code").HasColumnType("character varying(64)").IsRequired().HasMaxLength(64);
            b.Property<long>("Revision").HasColumnName("revision").HasColumnType("bigint").IsConcurrencyToken().HasDefaultValue(1L);
            b.Property<string>("PublicFingerprintSha256").HasColumnName("public_fingerprint_sha256").HasColumnType("character varying(64)").HasMaxLength(64);
            b.Property<DateTime>("OccurredAtUtc").HasColumnName("occurred_at_utc").HasColumnType("timestamp with time zone");
            b.HasKey("Id").HasName("signing_audit_events_pkey");
            b.HasIndex(new[] { "CustomerUserId", "TaxpayerId", "OccurredAtUtc", "Id" }, "ix_signing_audit_scope_time").HasDatabaseName("ix_signing_audit_scope_time");
            b.HasIndex(new[] { "SubjectKind", "SubjectId", "Revision" }, "ux_signing_audit_subject_revision").HasDatabaseName("ux_signing_audit_subject_revision").IsUnique();
            b.HasIndex(new[] { "ActorUserId" }, "ix_p09_audit_fk1").HasDatabaseName("ix_p09_audit_fk1");
            b.HasIndex(new[] { "OperationRecordId", "CustomerUserId", "TaxpayerId", "FiscalMemoryId" }, "ix_p09_audit_fk2").HasDatabaseName("ix_p09_audit_fk2");
            b.ToTable("signing_audit_events", t =>
            {
                t.HasCheckConstraint("ck_p09_audit_1", "subject_kind BETWEEN 1 AND 5");
                t.HasCheckConstraint("ck_p09_audit_2", "revision > 0");
                t.HasCheckConstraint("ck_p09_audit_3", "public_fingerprint_sha256 ~ '^[0-9a-f]{64}$'");
            });
        });
        modelBuilder.Entity<SigningOperation>(b =>
        {
            b.HasOne<global::ArginTaxPayer.Infrastructure.Persistence.Taxpayers.TaxpayerProfile>().WithMany()
                .HasForeignKey("TaxpayerId")
                .HasPrincipalKey("Id")
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p09_op_taxpayer_id");
            b.HasOne<global::ArginTaxPayer.Infrastructure.Persistence.Taxpayers.FiscalMemory>().WithMany()
                .HasForeignKey("FiscalMemoryId")
                .HasPrincipalKey("Id")
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p09_op_fiscal_memory_id");
            b.HasOne<global::ArginTaxPayer.Infrastructure.Persistence.Identity.IdentityUser>().WithMany()
                .HasForeignKey("ActorUserId")
                .HasPrincipalKey("Id")
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("signing_operations_actor_user_id_fkey");
        });

        modelBuilder.Entity<SigningConsent>(b =>
        {
            b.HasOne<global::ArginTaxPayer.Infrastructure.Persistence.Identity.IdentityUser>().WithMany()
                .HasForeignKey("ActorUserId")
                .HasPrincipalKey("Id")
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("signing_consents_actor_user_id_fkey");
            b.HasOne<global::ArginTaxPayer.Infrastructure.Persistence.Signing.SigningOperation>().WithMany()
                .HasForeignKey("OperationRecordId", "CustomerUserId", "TaxpayerId", "FiscalMemoryId")
                .HasPrincipalKey("Id", "CustomerUserId", "TaxpayerId", "FiscalMemoryId")
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_signing_consent_operation");
        });

        modelBuilder.Entity<SigningKeyVersion>(b =>
        {
            b.HasOne<global::ArginTaxPayer.Infrastructure.Persistence.Signing.SigningOperation>().WithMany()
                .HasForeignKey("OriginOperationRecordId", "CustomerUserId", "TaxpayerId", "FiscalMemoryId")
                .HasPrincipalKey("Id", "CustomerUserId", "TaxpayerId", "FiscalMemoryId")
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_signing_key_origin");
        });

        modelBuilder.Entity<SigningKeySelection>(b =>
        {
            b.HasOne<global::ArginTaxPayer.Infrastructure.Persistence.Identity.IdentityUser>().WithMany()
                .HasForeignKey("SelectedByUserId")
                .HasPrincipalKey("Id")
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("signing_key_selections_selected_by_user_id_fkey");
            b.HasOne<global::ArginTaxPayer.Infrastructure.Persistence.Signing.SigningKeyVersion>().WithMany()
                .HasForeignKey("KeyVersionId", "CustomerUserId", "TaxpayerId", "FiscalMemoryId")
                .HasPrincipalKey("Id", "CustomerUserId", "TaxpayerId", "FiscalMemoryId")
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_signing_selection_key");
        });

        modelBuilder.Entity<SigningChangeRequest>(b =>
        {
            b.HasOne<global::ArginTaxPayer.Infrastructure.Persistence.Identity.IdentityUser>().WithMany()
                .HasForeignKey("RequestedByUserId")
                .HasPrincipalKey("Id")
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("signing_change_requests_requested_by_user_id_fkey");
            b.HasOne<global::ArginTaxPayer.Infrastructure.Persistence.Identity.IdentityUser>().WithMany()
                .HasForeignKey("ReviewedByUserId")
                .HasPrincipalKey("Id")
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("signing_change_requests_reviewed_by_user_id_fkey");
            b.HasOne<global::ArginTaxPayer.Infrastructure.Persistence.Signing.SigningKeyVersion>().WithMany()
                .HasForeignKey("CurrentKeyVersionId", "CustomerUserId", "TaxpayerId", "FiscalMemoryId")
                .HasPrincipalKey("Id", "CustomerUserId", "TaxpayerId", "FiscalMemoryId")
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_signing_review_key");
            b.HasOne<global::ArginTaxPayer.Infrastructure.Persistence.Signing.SigningOperation>().WithMany()
                .HasForeignKey("ConsumedByOperationRecordId", "CustomerUserId", "TaxpayerId", "FiscalMemoryId")
                .HasPrincipalKey("Id", "CustomerUserId", "TaxpayerId", "FiscalMemoryId")
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_signing_review_consumption");
        });

        modelBuilder.Entity<SigningAuditEvent>(b =>
        {
            b.HasOne<global::ArginTaxPayer.Infrastructure.Persistence.Identity.IdentityUser>().WithMany()
                .HasForeignKey("ActorUserId")
                .HasPrincipalKey("Id")
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("signing_audit_events_actor_user_id_fkey");
            b.HasOne<global::ArginTaxPayer.Infrastructure.Persistence.Signing.SigningOperation>().WithMany()
                .HasForeignKey("OperationRecordId", "CustomerUserId", "TaxpayerId", "FiscalMemoryId")
                .HasPrincipalKey("Id", "CustomerUserId", "TaxpayerId", "FiscalMemoryId")
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_signing_audit_operation");
        });
    }
}
