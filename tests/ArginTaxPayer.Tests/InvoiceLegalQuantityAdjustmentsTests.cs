using System.Text.Json;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class InvoiceLegalQuantityAdjustmentsTests
{
    [Phase09PostgresFact]
    public async Task Original_three_return_one_then_correct_two_to_one_in_both_environments()
    {
        foreach (var production in new[] { false, true })
        {
            var connection = Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")!;
            if (new NpgsqlConnectionStringBuilder(connection).Database != "argintaxpayer_test")
                throw new InvalidOperationException("Only the dedicated test database is permitted.");
            await using var db = new ArginTaxPayerDbContext(
                new DbContextOptionsBuilder<ArginTaxPayerDbContext>().UseNpgsql(connection).Options);
            await db.Database.OpenConnectionAsync();
            // Connection-local tables prevent any changes to the user's invoice history.
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TEMP TABLE invoice_documents (
                    id uuid,customer_user_id uuid,taxpayer_id uuid,snapshot_json jsonb);
                CREATE TEMP TABLE sandbox_invoice_legal_relations (
                    predecessor_invoice_id uuid,successor_invoice_id uuid,customer_user_id uuid,
                    taxpayer_id uuid,subject int,state int);
                CREATE TEMP TABLE authority_invoice_legal_relations
                    (LIKE sandbox_invoice_legal_relations);
                """);
            var owner = Guid.NewGuid();
            var taxpayer = Guid.NewGuid();
            var original = Guid.NewGuid();
            var returned = Guid.NewGuid();
            var line = new InvoiceEntryLine(Guid.NewGuid(), "کاغذ A4", 3, 100000, 0, 0,
                Guid.NewGuid(), "2", "2710000113478");
            var snapshot = JsonSerializer.Serialize(new { lines = new[] { line with { Quantity = 1 } } });
            var originalSnapshot = JsonSerializer.Serialize(new { lines = new[] { line } });
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO invoice_documents VALUES ({original},{owner},{taxpayer},CAST({originalSnapshot} AS jsonb));
                INSERT INTO invoice_documents VALUES ({returned},{owner},{taxpayer},CAST({snapshot} AS jsonb));
                INSERT INTO sandbox_invoice_legal_relations VALUES ({original},{returned},{owner},{taxpayer},4,1);
                INSERT INTO authority_invoice_legal_relations VALUES ({original},{returned},{owner},{taxpayer},4,1);
                """);

            var remaining = await InvoiceLegalQuantityAdjustments.AfterReturnsAsync(
                db, owner, taxpayer, original, [line], production, reserveOne: false, CancellationToken.None);
            Assert.Equal(2m, Assert.Single(remaining).Quantity);
            var edited = new[] { remaining[0] with { Quantity = 1 } };
            var correction = Phase10CorrectionValidation.IsQuantityCorrection(remaining, edited, false);
            Assert.True(correction);
            Assert.True(Phase10CorrectionValidation.AllowedLines(remaining, edited, correction));

            // Opening a fresh correction uses two, while resuming the saved edit retains one.
            var savedDraft = JsonSerializer.Serialize(new { lines = edited });
            var withBaseline = await InvoiceLegalQuantityAdjustments.CorrectionDraftWithBaselineAsync(
                db, owner, taxpayer, original, savedDraft, production, CancellationToken.None);
            using var draft = JsonDocument.Parse(withBaseline);
            Assert.Equal(2m, draft.RootElement.GetProperty("correctionBaselineQuantities")[0].GetDecimal());
            Assert.Equal(1m, draft.RootElement.GetProperty("lines")[0].GetProperty("Quantity").GetDecimal());

            var returnLimit = await InvoiceLegalQuantityAdjustments.AfterReturnsAsync(
                db, owner, taxpayer, original, [line], production, reserveOne: true, CancellationToken.None);
            Assert.Equal(1m, Assert.Single(returnLimit).Quantity);
        }
    }
}
