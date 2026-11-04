using ArginTaxPayer.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase18FreshCatalogMigrationOrderTests
{
    [Fact]
    public void Early_goods_format_migration_does_not_reference_a_missing_table()
    {
        var early = new AddOfficialGoodsCodeFormat();
        Assert.Empty(early.UpOperations);
    }

    [Fact]
    public void Catalog_foundation_creates_official_table_before_format_constraint()
    {
        var foundation = new AddPhase06CatalogFoundations();
        var sql = string.Join("\n", foundation.UpOperations.OfType<SqlOperation>()
            .Select(operation => operation.Sql));
        var tablePosition = sql.IndexOf("CREATE TABLE official_goods_service_entries", StringComparison.Ordinal);
        var constraintPosition = sql.IndexOf(
            "ADD CONSTRAINT ck_official_goods_code_format", StringComparison.Ordinal);
        Assert.True(tablePosition >= 0, "Official goods table creation is missing.");
        Assert.True(constraintPosition > tablePosition,
            "13-digit format constraint must be created after the table.");
        Assert.Contains("code ~ '^[0-9]{13}$'", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Validation_migration_still_checks_the_exact_constraint()
    {
        var validator = new ValidateOfficialGoodsServiceIdentifiers();
        var sql = string.Join("\n", validator.UpOperations.OfType<SqlOperation>()
            .Select(operation => operation.Sql));
        Assert.Contains("ck_official_goods_code_format", sql, StringComparison.Ordinal);
    }
}
