using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261014002000_AllowLegacyCommercialAcquisition")]
public sealed class AllowLegacyCommercialAcquisition : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.DropCheckConstraint("ck_customer_owner_channel", "customer_commercial_owners");
        m.AddCheckConstraint("ck_customer_owner_channel", "customer_commercial_owners",
            "acquisition_channel IN (1, 2, 3, 4, 5)");
    }

    protected override void Down(MigrationBuilder m)
    {
        m.DropCheckConstraint("ck_customer_owner_channel", "customer_commercial_owners");
        m.AddCheckConstraint("ck_customer_owner_channel", "customer_commercial_owners",
            "acquisition_channel IN (1, 2, 3, 4)");
    }
}
