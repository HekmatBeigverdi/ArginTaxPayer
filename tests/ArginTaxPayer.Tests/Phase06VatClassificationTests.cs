using ArginTaxPayer.Domain.Catalog;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Catalog;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase06VatClassificationTests
{
    private static readonly DateTime Start = new(2026, 3, 21, 0, 0, 0, DateTimeKind.Utc);
    private static VatRule Rule(
        VatTreatment treatment, decimal? rate, DateTime? from = null,
        DateTime? to = null, bool verified = true, bool active = true,
        string? context = null) => new(Guid.NewGuid(), Guid.NewGuid(), "sample",
            treatment, rate, "reviewed source", from ?? Start, to, verified, active, context);

    [Fact]
    public void Validates_treatment_and_rate_without_a_global_default()
    {
        Assert.True(VatClassificationPolicy.IsValidRule(Rule(VatTreatment.Unresolved, null)));
        Assert.True(VatClassificationPolicy.IsValidRule(Rule(VatTreatment.Exempt, 0m)));
        Assert.True(VatClassificationPolicy.IsValidRule(Rule(VatTreatment.ZeroRated, 0m)));
        Assert.True(VatClassificationPolicy.IsValidRule(Rule(VatTreatment.Special, 15.25m)));
        Assert.True(VatClassificationPolicy.IsValidRule(Rule(VatTreatment.Standard, 10m)));
        Assert.False(VatClassificationPolicy.IsValidRule(Rule(VatTreatment.Unresolved, 0m)));
        Assert.False(VatClassificationPolicy.IsValidRule(Rule(VatTreatment.Exempt, null)));
        Assert.False(VatClassificationPolicy.IsValidRule(Rule(VatTreatment.Exempt, 10m)));
        Assert.False(VatClassificationPolicy.IsValidRule(Rule(VatTreatment.ZeroRated, 10m)));
        Assert.False(VatClassificationPolicy.IsValidRule(Rule(VatTreatment.Standard, null)));
        Assert.False(VatClassificationPolicy.IsValidRate(-1m));
        Assert.False(VatClassificationPolicy.IsValidRate(1000m));
        Assert.False(VatClassificationPolicy.IsValidRate(10.123m));
    }

    [Fact]
    public void Resolves_half_open_validity_windows_without_retroactive_rate_changes()
    {
        var cutover = Start.AddDays(10);
        var old = Rule(VatTreatment.Standard, 9m, to: cutover);
        var next = Rule(VatTreatment.Standard, 10m, from: cutover);
        var rules = new[] { old, next };
        Assert.Equal(old, VatClassificationPolicy.Resolve(rules, "sample", Start).Rule);
        Assert.Equal(old, VatClassificationPolicy.Resolve(rules, "sample", cutover.AddTicks(-1)).Rule);
        Assert.Equal(next, VatClassificationPolicy.Resolve(rules, "sample", cutover).Rule);
        Assert.Equal(VatResolutionStatus.Unresolved,
            VatClassificationPolicy.Resolve(rules, "sample", Start.AddDays(-1)).Status);
    }

    [Fact]
    public void Rejects_overlaps_and_unverified_ambiguous_or_missing_context()
    {
        var one = Rule(VatTreatment.Standard, 10m);
        var two = Rule(VatTreatment.Special, 15m);
        Assert.Equal(VatResolutionStatus.Ambiguous,
            VatClassificationPolicy.Resolve(new[] { one, two }, "sample", Start).Status);
        Assert.Equal(VatResolutionStatus.Unresolved,
            VatClassificationPolicy.Resolve(new[] { Rule(VatTreatment.Standard, 10m, verified: false) }, "sample", Start).Status);
        Assert.Equal(VatResolutionStatus.Unresolved,
            VatClassificationPolicy.Resolve(new[] { Rule(VatTreatment.Standard, 10m, active: false) }, "sample", Start).Status);
        Assert.Equal(VatResolutionStatus.Unresolved,
            VatClassificationPolicy.Resolve(new[] { one }, "other", Start).Status);
        Assert.Equal(VatResolutionStatus.InvalidContext,
            VatClassificationPolicy.Resolve(new[] { one }, "sample", DateTime.SpecifyKind(Start, DateTimeKind.Local)).Status);
        Assert.Equal(VatResolutionStatus.Unresolved,
            VatClassificationPolicy.Resolve(new[] { Rule(VatTreatment.Unresolved, null) }, "sample", Start).Status);
        Assert.Equal(VatResolutionStatus.Unresolved,
            VatClassificationPolicy.Resolve(new[] { Rule(VatTreatment.Standard, 10m, context: "export") }, "sample", Start).Status);
        Assert.Equal(VatResolutionStatus.Resolved,
            VatClassificationPolicy.Resolve(new[] { Rule(VatTreatment.ZeroRated, 0m, context: "export") }, "sample", Start, "export").Status);
    }

    [Fact]
    public void Invalid_windows_and_unspecified_legal_basis_are_rejected()
    {
        Assert.False(VatClassificationPolicy.IsValidRule(Rule(VatTreatment.Standard, 10m, to: Start)));
        Assert.False(VatClassificationPolicy.IsValidRule(Rule(VatTreatment.Standard, 10m, from: Start.AddDays(1), to: Start)));
        Assert.False(VatClassificationPolicy.IsValidRule(new VatRule(
            Guid.NewGuid(), Guid.NewGuid(), "x", VatTreatment.Standard, 10m, "",
            Start, null, true, true)));
    }

    [Fact]
    public void Ef_model_has_treatment_and_nullable_context()
    {
        using var db = new ArginTaxPayerDbContext(
            new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
                .UseNpgsql("Host=localhost;Database=never_used;Username=never_used;Password=never_used").Options);
        var entity = db.Model.FindEntityType(typeof(VatClassificationEntry))!;
        Assert.Equal("treatment", entity.FindProperty(nameof(VatClassificationEntry.Treatment))!.GetColumnName());
        Assert.True(entity.FindProperty(nameof(VatClassificationEntry.ContextKey))!.IsNullable);
        Assert.Equal(120, entity.FindProperty(nameof(VatClassificationEntry.ContextKey))!.GetMaxLength());
    }
}
