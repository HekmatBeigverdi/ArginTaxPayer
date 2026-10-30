using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10SandboxSingleInvoiceConsentTests
{
    private static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Taxpayer = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Invoice = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Memory = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 14, 0, 0, TimeSpan.Zero);
    private static readonly string Hash = new string('a', 64);
    private static SandboxSingleInvoiceConsent Valid() =>
        new(Owner, Taxpayer, Invoice, Memory, 4, Hash, Now.AddMinutes(2), true, "Sandbox");

    private static bool Check(SandboxSingleInvoiceConsent? consent, Guid? owner = null,
        Guid? invoice = null, long revision = 4, string? hash = null,
        DateTimeOffset? now = null) =>
        SandboxSingleInvoiceConsentGuard.Matches(consent, owner ?? Owner, Taxpayer,
            invoice ?? Invoice, Memory, revision, hash ?? Hash, now ?? Now);

    [Fact]
    public void Matching_short_lived_explicit_consent_can_pass_identity_guard()
        => Assert.True(Check(Valid()));

    [Fact]
    public void Missing_expired_and_not_confirmed_consent_are_rejected()
    {
        Assert.False(Check(null));
        Assert.False(Check(Valid() with { OperatorConfirmed = false }));
        Assert.False(Check(Valid() with { ExpiresAtUtc = Now }));
        Assert.False(Check(Valid() with { ExpiresAtUtc = Now.AddMinutes(6) }));
    }

    [Fact]
    public void Cross_owner_invoice_memory_revision_or_environment_never_reuses_consent()
    {
        Assert.False(Check(Valid(), owner: Guid.NewGuid()));
        Assert.False(Check(Valid(), invoice: Guid.NewGuid()));
        Assert.False(Check(Valid(), revision: 5));
        Assert.False(Check(Valid() with { SandboxFiscalMemoryId = Guid.NewGuid() }));
        Assert.False(Check(Valid() with { Environment = "Production" }));
        Assert.False(Check(Valid() with { TaxpayerId = Guid.NewGuid() }));
    }

    [Fact]
    public void Frozen_snapshot_digest_must_match()
    {
        Assert.True(Check(Valid(), hash: Hash.ToUpperInvariant()));
        Assert.False(Check(Valid(), hash: new string('b', 64)));
        Assert.False(Check(Valid() with { ApprovedSnapshotSha256 = "malformed" }));
        Assert.False(Check(Valid(), hash: "not-a-digest"));
    }
}
