using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase12SubscriptionCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subscription_plan_catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    validity_days = table.Column<int>(type: "integer", nullable: false),
                    invoice_unit_limit = table.Column<long>(type: "bigint", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_plan_catalog", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "subscription_price_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    root_admin_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    partner_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    audience = table.Column<int>(type: "integer", nullable: false),
                    price_irr = table.Column<long>(type: "bigint", nullable: false),
                    effective_from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_price_versions", x => x.id);
                    table.ForeignKey(
                        name: "FK_subscription_price_versions_subscription_plan_catalog_plan_~",
                        column: x => x.plan_id,
                        principalTable: "subscription_plan_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "customer_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    price_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    validity_days = table.Column<int>(type: "integer", nullable: false),
                    invoice_unit_limit = table.Column<long>(type: "bigint", nullable: true),
                    price_irr = table.Column<long>(type: "bigint", nullable: false),
                    root_admin_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    partner_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    state = table.Column<int>(type: "integer", nullable: false),
                    purchased_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    activated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "FK_customer_subscriptions_subscription_plan_catalog_plan_id",
                        column: x => x.plan_id,
                        principalTable: "subscription_plan_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_customer_subscriptions_subscription_price_versions_price_ve~",
                        column: x => x.price_version_id,
                        principalTable: "subscription_price_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_customer_subscriptions_customer_user_id_purchased_at_utc",
                table: "customer_subscriptions",
                columns: new[] { "customer_user_id", "purchased_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_subscriptions_plan_id",
                table: "customer_subscriptions",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_customer_subscriptions_price_version_id",
                table: "customer_subscriptions",
                column: "price_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_subscription_plan_catalog_code",
                table: "subscription_plan_catalog",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subscription_price_versions_plan_id",
                table: "subscription_price_versions",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_subscription_price_versions_root_admin_user_id_partner_user~",
                table: "subscription_price_versions",
                columns: new[] { "root_admin_user_id", "partner_user_id", "plan_id", "effective_from_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_subscriptions");

            migrationBuilder.DropTable(
                name: "subscription_price_versions");

            migrationBuilder.DropTable(
                name: "subscription_plan_catalog");
        }
    }
}
