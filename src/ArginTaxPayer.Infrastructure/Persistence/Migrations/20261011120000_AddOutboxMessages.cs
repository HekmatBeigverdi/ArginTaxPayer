using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261011120000_AddOutboxMessages")]
public sealed class AddOutboxMessages : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "outbox_messages",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                taxpayer_id = table.Column<Guid>(type: "uuid", nullable: false),
                event_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                payload_json = table.Column<string>(type: "text", nullable: false),
                deduplication_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                processed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
            },
            constraints: table => table.PrimaryKey("pk_outbox_messages", x => x.id));

        migrationBuilder.CreateIndex(
            name: "ux_outbox_taxpayer_deduplication",
            table: "outbox_messages",
            columns: new[] { "taxpayer_id", "deduplication_key" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "ix_outbox_pending",
            table: "outbox_messages",
            columns: new[] { "processed_at_utc", "created_at_utc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "outbox_messages");
    }

    protected override void BuildTargetModel(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "9.0.4");

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
