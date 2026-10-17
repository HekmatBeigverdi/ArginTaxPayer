using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261016190000_AddVatTreatmentAndContext")]
public sealed class AddVatTreatmentAndContext : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        // Existing rows require human classification rather than unsafe implicit assignment.
        m.Sql("""
DO $$
BEGIN
 IF EXISTS (SELECT 1 FROM vat_classification_entries) THEN
    RAISE EXCEPTION 'Existing VAT rows require explicit migration review';
 END IF;
END $$;
""");
        m.AddColumn<int>("treatment", "vat_classification_entries", "integer", nullable: false, defaultValue: 0);
        m.AddColumn<string>("context_key", "vat_classification_entries",
            "character varying(120)", maxLength: 120, nullable: true);
        m.DropCheckConstraint("ck_vat_percentage", "vat_classification_entries");
        m.AddCheckConstraint("ck_vat_percentage", "vat_classification_entries",
            "rate_percent IS NULL OR (rate_percent >= 0 AND rate_percent <= 999.99 AND rate_percent = round(rate_percent, 2))");
        m.AddCheckConstraint("ck_vat_treatment", "vat_classification_entries", "treatment BETWEEN 0 AND 4");
        m.AddCheckConstraint("ck_vat_treatment_rate", "vat_classification_entries",
            "(treatment = 0 AND rate_percent IS NULL) OR (treatment IN (2,3) AND rate_percent = 0) OR (treatment IN (1,4) AND rate_percent IS NOT NULL)");
    }

    protected override void Down(MigrationBuilder m)
    {
        m.Sql("""
DO $$
BEGIN
 IF EXISTS (SELECT 1 FROM vat_classification_entries) THEN
    RAISE EXCEPTION 'Existing VAT rows prevent safe rollback';
 END IF;
END $$;
""");
        m.DropCheckConstraint("ck_vat_treatment_rate", "vat_classification_entries");
        m.DropCheckConstraint("ck_vat_treatment", "vat_classification_entries");
        m.DropCheckConstraint("ck_vat_percentage", "vat_classification_entries");
        m.AddCheckConstraint("ck_vat_percentage", "vat_classification_entries",
            "rate_percent IS NULL OR (rate_percent >= 0 AND rate_percent <= 100)");
        m.DropColumn("context_key", "vat_classification_entries");
        m.DropColumn("treatment", "vat_classification_entries");
    }
}
