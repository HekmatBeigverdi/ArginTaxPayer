using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Parties;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

/// <summary>Offline EF/Npgsql SQL translation and constraint regression.
/// Does not connect to PostgreSQL or rely on opted-in test credentials.</summary>
public sealed class Phase05Step14QuerySecurityTests
{
    private static ArginTaxPayerDbContext Model() => new(
        new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata_only;Username=unused;Password=unused").Options);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Taxpayer_read_and_write_queries_enforce_both_current_owner_paths(bool root)
    {
        using var db = Model();
        var scope = new TaxpayerScopeService(db, null!, null!);
        var actor = Guid.NewGuid();
        foreach (var sql in new[]
        {
            scope.ScopedQuery(actor, root).ToQueryString(),
            scope.ScopedCommandQuery(actor, root).ToQueryString()
        })
        {
            Console.WriteLine("Phase05 Step14 scoped SQL: " + sql);
            Assert.Contains("customer_commercial_owners", sql);
            Assert.Contains("root_level1_user_id", sql);
            Assert.Contains("partner_level2_user_id", sql);
            Assert.Contains("identity_mfa_states", sql);
            Assert.Contains("identity_users", sql);
            Assert.DoesNotContain("taxpayer_delegate_grants", sql);
        }
    }

    [Fact]
    public void Fiscal_master_data_and_account_identity_remain_separate()
    {
        using var db = Model();
        var parties = db.Model.FindEntityType(typeof(Party))!;
        var links = db.Model.FindEntityType(typeof(CustomerPartyLink))!;
        var taxpayers = db.Model.FindEntityType(typeof(TaxpayerProfile))!;
        Assert.Equal("parties", parties.GetTableName());
        Assert.Equal("customer_party_links", links.GetTableName());
        Assert.Equal("taxpayer_profiles", taxpayers.GetTableName());
        Assert.All(links.GetForeignKeys(),
            fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
        Assert.True(parties.FindProperty(nameof(Party.Revision))!.IsConcurrencyToken);
        Assert.True(taxpayers.FindProperty(nameof(TaxpayerProfile.Revision))!.IsConcurrencyToken);
    }

    [Fact]
    public void Audit_and_draft_metadata_have_restrictive_relationships_and_revisions()
    {
        using var db = Model();
        var history = db.Model.FindEntityType(typeof(PartyIdentityHistory))!;
        var details = db.Model.FindEntityType(typeof(InvoiceParticipantDetails))!;
        Assert.Equal("party_identity_history", history.GetTableName());
        Assert.Equal("invoice_participant_details", details.GetTableName());
        Assert.All(history.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
        Assert.All(details.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
        Assert.True(details.FindProperty(nameof(InvoiceParticipantDetails.Revision))!.IsConcurrencyToken);
    }
}
