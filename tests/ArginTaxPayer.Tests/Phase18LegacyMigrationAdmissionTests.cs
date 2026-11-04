using ArginTaxPayer.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ArginTaxPayer.Tests;

/// <summary>
/// Old signing and sandbox migrations must remain restricted to exactly the
/// historical test DB, dedicated Phase18 QA over SSL, or new Production migrator
/// on the isolated Production database over SSL. Never the runtime app role.
/// </summary>
public sealed class Phase18LegacyMigrationAdmissionTests
{
    private static readonly Type[] GuardedMigrations =
    [
        typeof(AddPhase09SigningMetadata),
        typeof(AddEncryptedDatabaseSigningCustody),
        typeof(AllowDatabaseSigningCustodyReference),
        typeof(AddPhase10InvoiceDispatchOutbox),
        typeof(AddSandboxFiscalMemoryMetadata),
        typeof(AddSandboxEncryptedCustodyColumns),
        typeof(AddSandboxInvoiceFiscalReservations),
        typeof(AddSandboxInvoiceLifecycleEvents),
        typeof(AlignSandboxDispatchMemoryForeignKey),
        typeof(AddLegacyAuthorityInquiryReports)
    ];

    [Fact]
    public void Guarded_migrations_require_exact_QA_or_Production_migrator_and_TLS()
    {
        foreach (var type in GuardedMigrations)
        {
            var migration = (Migration)Activator.CreateInstance(type)!;
            var sql = string.Join("\n", migration.UpOperations
                .OfType<SqlOperation>().Select(o => o.Sql));

            AssertIsolatedDatabaseAdmission(sql);
        }
    }

    internal static void AssertIsolatedDatabaseAdmission(string sql, bool allowProduction = true)
    {
        var productionBranch = allowProduction
            ? @"OR\s*\(\s*current_database\(\)\s*=\s*'argintaxpayer_prod'\s*" +
              @"AND\s+current_user\s*=\s*'argin_prod_migrator'\s*" +
              @"AND\s+EXISTS\s*\(\s*SELECT\s+1\s+FROM\s+pg_stat_ssl\s*" +
              @"WHERE\s+pid\s*=\s*pg_backend_pid\(\)\s+AND\s+ssl\s*\)\s*\)\s*"
            : "";
        Assert.Matches(@"IF\s+NOT\s*\(\s*current_database\(\)\s*=\s*'argintaxpayer_test'\s*" +
            @"OR\s*\(\s*current_database\(\)\s*=\s*'argin_phase18_acceptance'\s*" +
            @"AND\s+current_user\s*=\s*'phase18_acceptance'\s*" +
            @"AND\s+EXISTS\s*\(\s*SELECT\s+1\s+FROM\s+pg_stat_ssl\s*" +
            @"WHERE\s+pid\s*=\s*pg_backend_pid\(\)\s+AND\s+ssl\s*\)\s*\)\s*" +
            productionBranch + @"\)\s*THEN\s+RAISE\s+EXCEPTION", sql);
        Assert.DoesNotContain("current_user = 'argin_prod_app'", sql);
    }
}
