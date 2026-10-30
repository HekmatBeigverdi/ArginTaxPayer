using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10EffectiveInvoiceTests
{
    [Phase09PostgresFact]
    public async Task Latest_submitted_correction_is_selected_before_authority_acceptance_across_multiple_links()
    {
        await using var db = await Database();
        var owner = Guid.NewGuid(); var taxpayer = Guid.NewGuid();
        var root = Guid.NewGuid(); var first = Guid.NewGuid(); var last = Guid.NewGuid();
        await Link(db, root, first, owner, taxpayer);
        await Link(db, first, last, owner, taxpayer);
        var result = await Phase10EffectiveInvoiceQuery.ReadAsync(db, [root], [owner], [taxpayer], default);
        Assert.Equal(last, result[root].InvoiceId);
        Assert.Equal("Correction", result[root].Subject);
        Assert.Equal("ACKNOWLEDGED", result[root].Status);
        // Even a rejected correction must expose its own report instead of the
        // original's successful report. No submission is repeated by this query.
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO sandbox_authority_inquiry_reports VALUES ({Guid.NewGuid()},{last},{owner},{taxpayer},CAST({"{\"Status\":\"FAILED\"}"} AS jsonb),{DateTime.UtcNow})");
        result = await Phase10EffectiveInvoiceQuery.ReadAsync(db, [root], [owner], [taxpayer], default);
        Assert.Equal(last, result[root].InvoiceId);
        Assert.Equal("FAILED", result[root].Status);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE sandbox_authority_inquiry_reports SET report_json=CAST({"{\"Status\":\"UNKNOWN\"}"} AS jsonb)");
        result = await Phase10EffectiveInvoiceQuery.ReadAsync(db, [root], [owner], [taxpayer], default);
        Assert.Equal("ACKNOWLEDGED", result[root].Status);
        Assert.Empty(await Phase10EffectiveInvoiceQuery.ReadAsync(db, [root], [Guid.NewGuid()], [taxpayer], default));
        Assert.Empty(await Phase10EffectiveInvoiceQuery.ReadAsync(db, [root], [owner], [Guid.NewGuid()], default));
    }

    [Phase09PostgresFact]
    public async Task Unsent_successor_and_unaccepted_cancellation_do_not_replace_the_effective_document()
    {
        await using var db = await Database();
        var owner = Guid.NewGuid(); var taxpayer = Guid.NewGuid();
        var root = Guid.NewGuid(); var first = Guid.NewGuid(); var last = Guid.NewGuid();
        await Link(db, root, first, owner, taxpayer);
        await Link(db, first, last, owner, taxpayer, sent: false);
        var result = await Phase10EffectiveInvoiceQuery.ReadAsync(db, [root], [owner], [taxpayer], default);
        Assert.Equal(first, result[root].InvoiceId);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE invoice_documents SET subject=3 WHERE id={first}");
        Assert.Empty(await Phase10EffectiveInvoiceQuery.ReadAsync(db, [root], [owner], [taxpayer], default));
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO sandbox_authority_inquiry_reports VALUES ({Guid.NewGuid()},{first},{owner},{taxpayer},CAST({"{\"Status\":\"SUCCESS\"}"} AS jsonb),{DateTime.UtcNow})");
        result = await Phase10EffectiveInvoiceQuery.ReadAsync(db, [root], [owner], [taxpayer], default);
        Assert.Equal("Cancellation", result[root].Subject);
    }

    [Phase09PostgresFact]
    public async Task Cycles_terminate_and_foreign_owner_edges_are_not_followed()
    {
        await using var db = await Database();
        var owner = Guid.NewGuid(); var taxpayer = Guid.NewGuid();
        var root = Guid.NewGuid(); var first = Guid.NewGuid(); var foreign = Guid.NewGuid();
        await Link(db, root, first, owner, taxpayer);
        await Link(db, first, foreign, Guid.NewGuid(), taxpayer);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO sandbox_invoice_legal_relations VALUES ({Guid.NewGuid()},{first},{root},{owner},{taxpayer},{DateTime.UtcNow})");
        var result = await Phase10EffectiveInvoiceQuery.ReadAsync(db, [root], [owner], [taxpayer], default);
        Assert.Equal(first, result[root].InvoiceId);
    }

    private static async Task Link(ArginTaxPayerDbContext db, Guid predecessor, Guid successor, Guid owner, Guid taxpayer, bool sent = true)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO invoice_documents VALUES ({successor},{owner},{taxpayer},2,{DateTime.UtcNow})");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO sandbox_invoice_legal_relations VALUES ({Guid.NewGuid()},{predecessor},{successor},{owner},{taxpayer},{DateTime.UtcNow})");
        if (sent) await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO invoice_dispatch_operations VALUES ({Guid.NewGuid()},{successor},{owner},{taxpayer},5,{DateTime.UtcNow})");
    }

    private static async Task<ArginTaxPayerDbContext> Database()
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")!;
        Assert.Equal("argintaxpayer_test", new NpgsqlConnectionStringBuilder(cs).Database);
        var db = new ArginTaxPayerDbContext(new DbContextOptionsBuilder<ArginTaxPayerDbContext>().UseNpgsql(cs).Options);
        await db.Database.OpenConnectionAsync();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TEMP TABLE invoice_documents(id uuid,customer_user_id uuid,taxpayer_id uuid,subject int,issue_date_utc timestamptz);
            CREATE TEMP TABLE sandbox_invoice_legal_relations(id uuid,predecessor_invoice_id uuid,successor_invoice_id uuid,customer_user_id uuid,taxpayer_id uuid,created_at_utc timestamptz);
            CREATE TEMP TABLE invoice_dispatch_operations(id uuid,invoice_id uuid,customer_user_id uuid,taxpayer_id uuid,state int,created_at_utc timestamptz);
            CREATE TEMP TABLE sandbox_authority_inquiry_reports(id uuid,invoice_id uuid,customer_user_id uuid,taxpayer_id uuid,report_json jsonb,observed_at_utc timestamptz);
            """);
        return db;
    }
}
