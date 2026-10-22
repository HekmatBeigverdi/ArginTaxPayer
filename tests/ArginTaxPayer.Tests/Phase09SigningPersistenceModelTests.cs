using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Signing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace ArginTaxPayer.Tests;

/// <summary>Model and generated-script tests only. No connection is opened and no migration is applied.</summary>
public sealed class Phase09SigningPersistenceModelTests
{
    private const string MigrationId = "20261020181500_AddPhase09SigningMetadata";
    private const string DatabaseCustodyConstraintMigrationId =
        "20261022150000_AllowDatabaseSigningCustodyReference";
    private static ArginTaxPayerDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=argintaxpayer_test;Username=unused;Password=unused").Options);

    private static readonly Type[] SigningTypes =
    [
        typeof(SigningOperation), typeof(SigningConsent), typeof(SigningKeyVersion),
        typeof(SigningKeySelection), typeof(SigningChangeRequest), typeof(SigningAuditEvent)
    ];

    [Fact]
    public void Six_metadata_entities_have_explicit_tables_and_restrictive_foreign_keys()
    {
        using var db = CreateContext();
        foreach (var type in SigningTypes)
        {
            var entity = db.Model.FindEntityType(type)!;
            Assert.StartsWith("signing_", entity.GetTableName());
            Assert.NotEmpty(entity.GetForeignKeys());
            Assert.All(entity.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
            Assert.DoesNotContain(entity.GetProperties(), p => p.IsShadowProperty());
        }
    }

    [Fact]
    public void Secret_and_envelope_columns_are_absent_but_public_key_bytes_are_allowed()
    {
        using var db = CreateContext();
        var forbidden = new[] { "private", "password", "ciphertext", "wrapped_dek", "secret", "jwt" };
        foreach (var type in SigningTypes)
        foreach (var property in db.Model.FindEntityType(type)!.GetProperties())
        {
            var name = property.GetColumnName()!.ToLowerInvariant();
            Assert.DoesNotContain(forbidden, word => name.Contains(word));
            if (property.ClrType == typeof(byte[]))
                Assert.Equal("PublicSpkiDer", property.Name);
        }
    }

    [Fact]
    public void Scoped_references_include_customer_taxpayer_and_memory()
    {
        using var db = CreateContext();
        foreach (var type in SigningTypes.Where(t => t != typeof(SigningOperation)))
        {
            Assert.Contains(db.Model.FindEntityType(type)!.GetForeignKeys(), fk =>
                fk.Properties.Select(p => p.Name).Contains("CustomerUserId") &&
                fk.Properties.Select(p => p.Name).Contains("TaxpayerId") &&
                fk.Properties.Select(p => p.Name).Contains("FiscalMemoryId"));
        }
        // Nullable legacy ownership is not promoted to an EF alternate key.
        var taxpayer = db.Model.GetEntityTypes().Single(e => e.GetTableName() == "taxpayer_profiles");
        Assert.True(taxpayer.FindProperty("CustomerUserId")!.IsNullable);
    }

    [Fact]
    public void One_selection_per_memory_keeps_revision_when_unselected()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(SigningKeySelection))!;
        Assert.Equal(new[] { "FiscalMemoryId" }, entity.FindPrimaryKey()!.Properties.Select(p => p.Name));
        Assert.True(entity.FindProperty("KeyVersionId")!.IsNullable);
        Assert.True(entity.FindProperty("Revision")!.IsConcurrencyToken);
    }

    [Fact]
    public void Mutable_entities_have_optimistic_concurrency_tokens()
    {
        using var db = CreateContext();
        foreach (var type in new[] { typeof(SigningOperation), typeof(SigningKeyVersion),
                     typeof(SigningKeySelection), typeof(SigningChangeRequest) })
            Assert.True(db.Model.FindEntityType(type)!.FindProperty("Revision")!.IsConcurrencyToken);
    }

    [Fact]
    public void Fingerprints_are_not_global_unique_identity_keys()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(SigningKeyVersion))!;
        Assert.DoesNotContain(entity.GetIndexes(), i =>
            i.IsUnique && i.Properties.Any(p => p.Name == "PublicFingerprintSha256"));
        Assert.DoesNotContain(entity.GetKeys(), k =>
            k.Properties.Any(p => p.Name == "PublicFingerprintSha256"));
        Assert.Contains(db.Model.FindEntityType(typeof(SigningOperation))!.GetKeys(), k =>
            k.Properties.Select(p => p.Name).SequenceEqual(
                new[] { "CustomerUserId", "TaxpayerId", "FiscalMemoryId", "OperationId" }));
    }

    [Fact]
    public void Null_consumption_reference_does_not_become_required()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(SigningChangeRequest))!;
        Assert.True(entity.FindProperty("ConsumedByOperationRecordId")!.IsNullable);
        Assert.Contains(entity.GetIndexes(), i => i.IsUnique &&
            i.GetFilter() == "consumed_by_operation_record_id IS NOT NULL");
    }

    [Fact]
    public void Runtime_model_matches_the_committed_snapshot()
    {
        using var db = CreateContext();
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Custody_reference_constraint_accepts_database_and_vault_locators()
    {
        using var db = CreateContext();
        var entity = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(SigningKeyVersion))!;
        var constraint = entity.GetCheckConstraints().Single(c => c.Name == "ck_p09_key_5");

        Assert.Contains("(vault|db)", constraint.Sql);
        Assert.Contains(DatabaseCustodyConstraintMigrationId, db.Database.GetMigrations());

        var script = db.GetService<IMigrator>().GenerateScript(
            "20261022140000_AddEncryptedDatabaseSigningCustody",
            DatabaseCustodyConstraintMigrationId);
        Assert.Contains("DROP CONSTRAINT IF EXISTS ck_p09_key_5", script);
        Assert.Contains("^(vault|db)://", script);
    }

    [Fact]
    public void Migration_is_discoverable_and_generates_guarded_additive_sql_without_connecting()
    {
        using var db = CreateContext();
        Assert.Contains(MigrationId, db.Database.GetMigrations());
        var script = db.GetService<IMigrator>().GenerateScript(
            "20261020104500_AddInvoiceWorkspaceRevisionAudit", MigrationId);
        Assert.Contains("current_database() <> 'argintaxpayer_test'", script);
        foreach (var table in new[] { "signing_operations", "signing_consents", "signing_key_versions",
                     "signing_key_selections", "signing_change_requests", "signing_audit_events" })
            Assert.Contains("CREATE TABLE " + table, script);
        Assert.DoesNotContain("ALTER TABLE fiscal_certificate_metadata", script);
        Assert.DoesNotContain("UPDATE taxpayer_profiles", script);
        Assert.DoesNotContain("DELETE FROM", script);
        Assert.DoesNotContain("DROP TABLE", script);
        Assert.Contains("REFERENCES taxpayer_profiles(id, customer_user_id)", script);
        Assert.Contains("REFERENCES fiscal_memories(id, taxpayer_id)", script);
        Assert.Contains("DEFERRABLE INITIALLY DEFERRED", script);
        Assert.Contains("BEFORE TRUNCATE", script);
        Assert.Contains("phase09_require_consent", script);
        Assert.Contains("phase09_require_audit", script);
        Assert.Contains("phase09_selection_lock", script);
    }

    [Fact]
    public void Migration_target_model_contains_all_six_signing_entities()
    {
        using var db = CreateContext();
        var assembly = db.GetService<IMigrationsAssembly>();
        var migration = assembly.CreateMigration(assembly.Migrations[MigrationId], db.Database.ProviderName!);
        foreach (var type in SigningTypes)
            Assert.NotNull(migration.TargetModel.FindEntityType(type.FullName!));
    }

    [Fact]
    public void Downgrade_refuses_nonempty_history_and_never_cascades()
    {
        using var db = CreateContext();
        var script = db.GetService<IMigrator>().GenerateScript(
            MigrationId, "20261020104500_AddInvoiceWorkspaceRevisionAudit");
        Assert.Contains("Nonempty Phase09 history", script);
        Assert.Contains("current_database() <> 'argintaxpayer_test'", script);
        Assert.DoesNotContain("CASCADE", script);
    }
}
