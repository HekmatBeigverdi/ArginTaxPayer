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
        // The preceding 20261016082706_AddOfficialGoodsCodeFormat migration
        // owns and creates the CHECK. This migration only verifies its presence.
        // Never drop/recreate a constraint already applied to customer data.
        migrationBuilder.Sql("""
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
          FROM pg_constraint c
         WHERE c.conrelid = 'public.official_goods_service_entries'::regclass
           AND c.conname = 'ck_official_goods_code_format'
           AND c.contype = 'c'
           AND pg_get_constraintdef(c.oid) LIKE '%^[0-9]{13}$%'
    ) THEN
        RAISE EXCEPTION 'Expected 13-digit official goods/service CHECK is missing or differs';
    END IF;
END
$$;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The preceding migration owns this constraint. Do not drop it here.
    }
}
