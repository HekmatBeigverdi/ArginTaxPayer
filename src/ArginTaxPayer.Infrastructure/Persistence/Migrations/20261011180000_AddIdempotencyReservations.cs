using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261011180000_AddIdempotencyReservations")]
public sealed class AddIdempotencyReservations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "idempotency_reservations",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                taxpayer_id = table.Column<Guid>(type: "uuid", nullable: false),
                operation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                request_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_idempotency_reservations", x => x.id));

        migrationBuilder.CreateIndex(
            name: "ux_idempotency_taxpayer_operation_key",
            table: "idempotency_reservations",
            columns: new[] { "taxpayer_id", "operation", "idempotency_key" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "idempotency_reservations");
    }

    protected override void BuildTargetModel(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "9.0.4");
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
