using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261016150000_ValidateOfficialGoodsServiceIdentifiers")]
public sealed class ValidateOfficialGoodsServiceIdentifiers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Defensive: fail instead of silently changing any manually imported historical code.
        migrationBuilder.Sql("""
ALTER TABLE official_goods_service_entries
ADD CONSTRAINT ck_official_goods_code_format
CHECK (code ~ '^[0-9]{13}$');
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
ALTER TABLE official_goods_service_entries
DROP CONSTRAINT ck_official_goods_code_format;
""");
    }
}
