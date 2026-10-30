using System.Reflection;
using ArginTaxPayer.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10DispatchDatabaseContractsTests
{
    [Fact]
    public void Migration_requires_isolated_test_database_and_additive_schema()
    {
        var migration = new AddPhase10InvoiceDispatchOutbox();
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        var up = typeof(AddPhase10InvoiceDispatchOutbox)
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!;
        up.Invoke(migration, new object[] { builder });
        var sql = string.Join("\n", builder.Operations.OfType<SqlOperation>()
            .Select(x => x.Sql));
        Assert.Contains("current_database() <> 'argintaxpayer_test'", sql);
        Assert.Contains("CREATE TABLE invoice_dispatch_operations", sql);
        Assert.Contains("CREATE TABLE invoice_dispatch_attempts", sql);
        Assert.Contains("CREATE TABLE invoice_dispatch_receipts", sql);
        Assert.Contains("ux_p10_dispatch_key", sql);
        Assert.Contains("ux_p10_dispatch_invoice_revision", sql);
        Assert.Contains("REFERENCES fiscal_memories(id) ON DELETE RESTRICT", sql);
        Assert.Contains("trg_p10_receipts_immutable", sql);
        Assert.Contains("trg_p10_attempts_immutable", sql);
        Assert.DoesNotContain("DROP TABLE invoice_documents", sql);
    }

    [Fact]
    public void Migration_disallows_automatic_destructive_rollback()
    {
        var migration = new AddPhase10InvoiceDispatchOutbox();
        var down = typeof(AddPhase10InvoiceDispatchOutbox)
            .GetMethod("Down", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var ex = Assert.Throws<TargetInvocationException>(() =>
            down.Invoke(migration, new object[] { new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL") }));
        Assert.IsType<NotSupportedException>(ex.InnerException);
    }

    [Fact]
    public void Existing_dispatch_idempotency_contract_preserves_scope_separation()
    {
        var scope = Guid.NewGuid();
        var customer = Guid.NewGuid();
        var taxpayer = Guid.NewGuid();
        var invoice = Guid.NewGuid();
        var a = ArginTaxPayer.Domain.Invoices.InvoiceDispatchAdmission.Create(
            scope, customer, taxpayer, Guid.NewGuid(), invoice, 9,
            new string('a',64), "synthetic-key");
        var b = ArginTaxPayer.Domain.Invoices.InvoiceDispatchAdmission.Create(
            Guid.NewGuid(), customer, taxpayer, a.FiscalMemoryId, invoice, 9,
            new string('a',64), "synthetic-key");
        Assert.Equal(ArginTaxPayer.Domain.Invoices.InvoiceAdmissionReplayDecision.ReplaySameOperation,
            a.CompareReplay(b));
        var conflict = ArginTaxPayer.Domain.Invoices.InvoiceDispatchAdmission.Create(
            Guid.NewGuid(),customer,taxpayer,a.FiscalMemoryId,invoice,10,
            new string('a',64),"synthetic-key");
        Assert.Equal(ArginTaxPayer.Domain.Invoices.InvoiceAdmissionReplayDecision.ConflictingPayload,
            a.CompareReplay(conflict));
    }
}
