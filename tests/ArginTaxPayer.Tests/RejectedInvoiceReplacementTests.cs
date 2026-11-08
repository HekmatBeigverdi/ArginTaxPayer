using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class RejectedInvoiceReplacementTests
{
    [Phase09PostgresFact]
    public async Task Draft_replacement_origin_is_durable_idempotent_and_scoped_before_promotion()
    {
        await using var db=await ViewDatabase();
        var owner=Guid.NewGuid();var taxpayer=Guid.NewGuid();var source=Guid.NewGuid();
        var workspace=Guid.NewGuid();var operation=Guid.NewGuid();var now=DateTime.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO invoice_documents VALUES ({source},{owner},{taxpayer},1)");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO invoice_dispatch_operations VALUES ({operation},{source},{owner},{taxpayer},1,{now})");
        await Report(db,owner,taxpayer,source,operation,now,"FAILED");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO invoice_entry_workspaces VALUES ({workspace},{owner},{taxpayer})");
        Assert.True(await RejectedInvoiceReplacementQuery.RegisterAsync(db,owner,taxpayer,source,operation,workspace,default));
        Assert.True(await RejectedInvoiceReplacementQuery.RegisterAsync(db,owner,taxpayer,source,operation,workspace,default));
        Assert.False(await RejectedInvoiceReplacementQuery.RegisterAsync(db,Guid.NewGuid(),taxpayer,source,operation,workspace,default));
        Assert.Equal(1,await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM invoice_rejection_replacements").SingleAsync());
        Assert.Equal(source,await RejectedInvoiceReplacementQuery.ResolveAsync(db,owner,taxpayer,source,default));
    }

    [Phase09PostgresFact]
    public async Task Rejected_original_is_hidden_only_after_its_replacement_is_confirmed_successful()
    {
        await using var db=await ViewDatabase();
        var owner=Guid.NewGuid();var taxpayer=Guid.NewGuid();var source=Guid.NewGuid();
        var replacement=Guid.NewGuid();var sourceOperation=Guid.NewGuid();var newOperation=Guid.NewGuid();
        var now=DateTime.UtcNow;
        await AddReplacement(db,owner,taxpayer,source,replacement,sourceOperation,newOperation,now,"IN_PROGRESS");
        Assert.Empty(await db.Database.SqlQueryRaw<Guid>("SELECT predecessor_invoice_id AS \"Value\" FROM invoice_successful_rejection_replacements").ToArrayAsync());
        Assert.Equal(source,await RejectedInvoiceReplacementQuery.ResolveAsync(db,owner,taxpayer,source,default));
        await Report(db,owner,taxpayer,replacement,newOperation,now.AddSeconds(1),"SUCCESS");
        var visible=await db.Database.SqlQueryRaw<Guid>("""
SELECT id AS "Value" FROM invoice_documents
WHERE id NOT IN(SELECT predecessor_invoice_id FROM invoice_successful_rejection_replacements)
""").ToArrayAsync();
        Assert.Equal(replacement,Assert.Single(visible));
        Assert.Equal(replacement,await RejectedInvoiceReplacementQuery.ResolveAsync(db,owner,taxpayer,source,default));
        Assert.Equal(2,await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM invoice_documents").SingleAsync());
    }

    [Phase09PostgresFact]
    public async Task Latest_successful_replacement_wins_and_prior_invoice_keeps_its_own_identity()
    {
        await using var db=await ViewDatabase();
        var owner=Guid.NewGuid();var taxpayer=Guid.NewGuid();var source=Guid.NewGuid();
        var earlier=Guid.NewGuid();var latest=Guid.NewGuid();var sourceOperation=Guid.NewGuid();
        var now=DateTime.UtcNow;
        await AddReplacement(db,owner,taxpayer,source,earlier,sourceOperation,Guid.NewGuid(),now,"SUCCESS");
        await AddReplacement(db,owner,taxpayer,source,latest,sourceOperation,Guid.NewGuid(),now.AddMinutes(1),"SUCCESS");
        Assert.Equal(latest,await RejectedInvoiceReplacementQuery.ResolveAsync(db,owner,taxpayer,source,default));
        Assert.Equal(earlier,await RejectedInvoiceReplacementQuery.ResolveAsync(db,owner,taxpayer,earlier,default));
        Assert.Equal(latest,await db.Database.SqlQueryRaw<Guid>("SELECT successor_invoice_id AS \"Value\" FROM invoice_successful_rejection_replacements").SingleAsync());
    }

    [Phase09PostgresFact]
    public async Task Successful_replacement_becomes_the_single_invoice_identity()
    {
        await using var db=await Database();
        var owner=Guid.NewGuid();var taxpayer=Guid.NewGuid();
        var rejected=Guid.NewGuid();var accepted=Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO invoice_successful_rejection_replacements VALUES ({owner},{taxpayer},{rejected},{accepted})");
        Assert.Equal(accepted,await RejectedInvoiceReplacementQuery.ResolveAsync(db,owner,taxpayer,rejected,default));
        Assert.Equal(accepted,await RejectedInvoiceReplacementQuery.ResolveAsync(db,owner,taxpayer,accepted,default));
        Assert.Equal(rejected,await RejectedInvoiceReplacementQuery.ResolveAsync(db,Guid.NewGuid(),taxpayer,rejected,default));
        Assert.Equal(rejected,await RejectedInvoiceReplacementQuery.ResolveAsync(db,owner,Guid.NewGuid(),rejected,default));
    }

    [Phase09PostgresFact]
    public async Task Replacement_resolution_does_not_follow_legal_successors_of_another_invoice()
    {
        await using var db=await Database();
        var owner=Guid.NewGuid();var taxpayer=Guid.NewGuid();
        var rejected=Guid.NewGuid();var accepted=Guid.NewGuid();var other=Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO invoice_successful_rejection_replacements VALUES ({owner},{taxpayer},{rejected},{accepted})");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO authority_invoice_legal_relations VALUES ({other},{Guid.NewGuid()})");
        Assert.Equal(accepted,await RejectedInvoiceReplacementQuery.ResolveAsync(db,owner,taxpayer,rejected,default));
        Assert.Equal(other,await RejectedInvoiceReplacementQuery.ResolveAsync(db,owner,taxpayer,other,default));
    }

    [Phase09PostgresFact]
    public async Task Successful_replacement_keeps_the_predecessor_human_invoice_number()
    {
        await using var db=await ViewDatabase();
        await db.Database.ExecuteSqlRawAsync("CREATE TEMP TABLE invoice_support_references(source_kind text,source_id uuid,system_code text)");
        var owner=Guid.NewGuid();var taxpayer=Guid.NewGuid();var source=Guid.NewGuid();
        var replacement=Guid.NewGuid();var sourceOperation=Guid.NewGuid();var newOperation=Guid.NewGuid();
        var now=DateTime.UtcNow;
        await AddReplacement(db,owner,taxpayer,source,replacement,sourceOperation,newOperation,now,"SUCCESS");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO invoice_support_references VALUES ('document',{source},'00004-05-ACR'),('document',{replacement},'00013-05-ACR')");

        var codes=await RejectedInvoiceReplacementQuery.ReadEffectiveSystemCodesAsync(db,
            new[]{replacement},new[]{owner},new[]{taxpayer},default);

        Assert.Equal("00004-05-ACR",codes[replacement]);
        Assert.Contains(replacement,await RejectedInvoiceReplacementQuery.FindBySystemCodeAsync(
            db,"00004-05-ACR",[taxpayer],default));
        Assert.DoesNotContain(replacement,await RejectedInvoiceReplacementQuery.FindBySystemCodeAsync(
            db,"00013-05-ACR",[taxpayer],default));
    }

    private static async Task<ArginTaxPayerDbContext> Database()
    {
        var cs=Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")!;
        Assert.Equal("argintaxpayer_test",new NpgsqlConnectionStringBuilder(cs).Database);
        var db=new ArginTaxPayerDbContext(new DbContextOptionsBuilder<ArginTaxPayerDbContext>().UseNpgsql(cs).Options);
        await db.Database.OpenConnectionAsync();
        await db.Database.ExecuteSqlRawAsync("""
CREATE TEMP TABLE invoice_successful_rejection_replacements(customer_user_id uuid,taxpayer_id uuid,predecessor_invoice_id uuid,successor_invoice_id uuid);
CREATE TEMP TABLE authority_invoice_legal_relations(predecessor_invoice_id uuid,successor_invoice_id uuid);
""");
        return db;
    }

    private static async Task<ArginTaxPayerDbContext> ViewDatabase()
    {
        var cs=Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")!;
        Assert.Equal("argintaxpayer_test",new NpgsqlConnectionStringBuilder(cs).Database);
        var db=new ArginTaxPayerDbContext(new DbContextOptionsBuilder<ArginTaxPayerDbContext>().UseNpgsql(cs).Options);
        await db.Database.OpenConnectionAsync();
        await db.Database.ExecuteSqlRawAsync("""
CREATE TEMP TABLE invoice_rejection_replacements(id uuid,customer_user_id uuid,taxpayer_id uuid,predecessor_invoice_id uuid,predecessor_operation_id uuid,successor_invoice_id uuid,created_at_utc timestamptz,UNIQUE(customer_user_id,taxpayer_id,successor_invoice_id));
CREATE TEMP TABLE invoice_documents(id uuid PRIMARY KEY,customer_user_id uuid,taxpayer_id uuid,subject int);
CREATE TEMP TABLE invoice_entry_workspaces(id uuid,customer_user_id uuid,taxpayer_id uuid);
CREATE TEMP TABLE invoice_dispatch_operations(id uuid PRIMARY KEY,invoice_id uuid,customer_user_id uuid,taxpayer_id uuid,invoice_revision bigint,created_at_utc timestamptz);
CREATE TEMP TABLE production_authority_inquiry_reports(id uuid,operation_id uuid,invoice_id uuid,customer_user_id uuid,taxpayer_id uuid,report_json jsonb,observed_at_utc timestamptz);
""");
        await db.Database.ExecuteSqlRawAsync(AddRejectedInvoiceReplacements.SuccessfulReplacementsViewSql.Replace("CREATE VIEW ","CREATE TEMP VIEW "));
        return db;
    }

    private static async Task AddReplacement(ArginTaxPayerDbContext db,Guid owner,Guid taxpayer,
        Guid source,Guid replacement,Guid sourceOperation,Guid newOperation,DateTime time,string status)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO invoice_documents VALUES ({source},{owner},{taxpayer},1),({replacement},{owner},{taxpayer},1) ON CONFLICT DO NOTHING");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO invoice_dispatch_operations VALUES ({sourceOperation},{source},{owner},{taxpayer},1,{time}),({newOperation},{replacement},{owner},{taxpayer},1,{time}) ON CONFLICT DO NOTHING");
        await Report(db,owner,taxpayer,source,sourceOperation,time,"FAILED");
        await Report(db,owner,taxpayer,replacement,newOperation,time,status);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO invoice_rejection_replacements VALUES ({Guid.NewGuid()},{owner},{taxpayer},{source},{sourceOperation},{replacement},{time})");
    }

    private static Task Report(ArginTaxPayerDbContext db,Guid owner,Guid taxpayer,Guid invoice,
        Guid operation,DateTime time,string status) => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO production_authority_inquiry_reports VALUES ({Guid.NewGuid()},{operation},{invoice},{owner},{taxpayer},CAST({System.Text.Json.JsonSerializer.Serialize(new {Status=status,CanPrepareCorrection=status=="FAILED"})} AS jsonb),{time})");
}
