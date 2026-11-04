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
            Assert.Contains("current_database() = 'argintaxpayer_test'", sql);
            Assert.Contains("current_database() = 'argin_phase18_acceptance'", sql);
            Assert.Contains("current_user = 'phase18_acceptance'", sql);
            Assert.Contains("pg_stat_ssl", sql);
            Assert.Contains("pg_backend_pid()", sql);
            Assert.Contains("AND ssl", sql);
        }
    }
}
