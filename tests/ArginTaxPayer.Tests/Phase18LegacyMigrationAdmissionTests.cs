using ArginTaxPayer.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ArginTaxPayer.Tests;

/// <summary>Ensures no migration relaxes access beyond the existing test DB
/// and the single TLS-protected, restricted Phase18 disposable database.</summary>
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
    public void Historically_test_only_migrations_admit_only_exact_disposable_TLS_identity()
    {
        foreach (var type in GuardedMigrations)
        {
            var migration = (Migration)Activator.CreateInstance(type)!;
            var sql = string.Join("\n", migration.UpOperations
                .OfType<SqlOperation>().Select(o => o.Sql));
            AssertIsolatedDatabaseAdmission(sql);
        }
    }

    internal static void AssertIsolatedDatabaseAdmission(string sql)
    {
        Assert.Matches(@"IF\s+NOT\s*\(\s*current_database\(\)\s*=\s*'argintaxpayer_test'\s*" +
            @"OR\s*\(\s*current_database\(\)\s*=\s*'argin_phase18_acceptance'\s*" +
            @"AND\s+current_user\s*=\s*'phase18_acceptance'\s*" +
            @"AND\s+EXISTS\s*\(\s*SELECT\s+1\s+FROM\s+pg_stat_ssl\s*" +
            @"WHERE\s+pid\s*=\s*pg_backend_pid\(\)\s+AND\s+ssl\s*\)\s*\)\s*\)\s*THEN\s+RAISE\s+EXCEPTION", sql);
    }
}
