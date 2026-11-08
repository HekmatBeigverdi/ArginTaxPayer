using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Historical migration ID must remain stable: it was generated before the
    /// catalog foundation migration which creates official_goods_service_entries.
    /// Applying its original AddCheckConstraint on a fresh DB fails with 42P01.
    /// The exact CHECK now belongs to AddPhase06CatalogFoundations, immediately
    /// after that table is created. Existing upgraded databases already contain
    /// the constraint and are not changed retroactively by this migration.
    /// </summary>
    public partial class AddOfficialGoodsCodeFormat : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally deferred to 20261016120000_AddPhase06CatalogFoundations.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The constraint is owned by the table's later foundation migration.
            // Never drop it while the catalog table remains in use.
        }
    }
}
