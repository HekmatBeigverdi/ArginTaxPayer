using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261012030000_AddIdentitySessions")]
public sealed class AddIdentitySessions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "identity_sessions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                access_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                refresh_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                previous_refresh_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                access_expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                refresh_expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                revoked_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                rotation_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_identity_sessions", x => x.id);
                table.ForeignKey("FK_identity_sessions_identity_users_user_id",
                    x => x.user_id, "identity_users", "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "ux_identity_sessions_access_hash",
            table: "identity_sessions", column: "access_token_hash", unique: true);
        migrationBuilder.CreateIndex(name: "ux_identity_sessions_refresh_hash",
            table: "identity_sessions", column: "refresh_token_hash", unique: true);
        migrationBuilder.CreateIndex(name: "ix_identity_sessions_previous_refresh_hash",
            table: "identity_sessions", column: "previous_refresh_token_hash");
        migrationBuilder.CreateIndex(name: "ix_identity_sessions_user_revoked",
            table: "identity_sessions", columns: new[] { "user_id", "revoked_at_utc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable("identity_sessions");

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "9.0.4");

        modelBuilder.Entity("ArginTaxPayer.Infrastructure.Persistence.Identity.IdentityUser", b =>
        {
            b.Property<Guid>("Id").HasColumnName("id").HasColumnType("uuid");
            b.Property<string>("NormalizedEmail").IsRequired().HasMaxLength(320).HasColumnName("normalized_email").HasColumnType("character varying(320)");
            b.Property<string>("PasswordHash").IsRequired().HasMaxLength(512).HasColumnName("password_hash").HasColumnType("character varying(512)");
            b.Property<bool>("IsActive").HasColumnName("is_active").HasColumnType("boolean");
            b.Property<DateTime>("CreatedAtUtc").HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
            b.Property<DateTime?>("PasswordChangedAtUtc").HasColumnName("password_changed_at_utc").HasColumnType("timestamp with time zone");
            b.HasKey("Id");
            b.HasIndex("NormalizedEmail").IsUnique().HasDatabaseName("ux_identity_users_email");
            b.ToTable("identity_users");
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
