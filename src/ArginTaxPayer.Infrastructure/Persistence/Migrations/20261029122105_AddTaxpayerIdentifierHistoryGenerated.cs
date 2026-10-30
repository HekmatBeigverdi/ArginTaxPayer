using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxpayerIdentifierHistoryGenerated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "new_normalized_identifier",
                table: "taxpayer_profile_changes",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "prior_normalized_identifier",
                table: "taxpayer_profile_changes",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "new_normalized_identifier",
                table: "taxpayer_profile_changes");

            migrationBuilder.DropColumn(
                name: "prior_normalized_identifier",
                table: "taxpayer_profile_changes");
        }
    }
}
