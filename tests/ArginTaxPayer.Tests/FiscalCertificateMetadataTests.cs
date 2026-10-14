using ArginTaxPayer.Domain.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class FiscalCertificateMetadataTests
{
    [Fact]
    public void Certificate_metadata_is_scoped_to_fiscal_memory()
    {
        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata_only;Username=unused;Password=unused").Options;
        using var db = new ArginTaxPayerDbContext(options);
        var entity = db.Model.FindEntityType(typeof(FiscalCertificateMetadata))!;
        Assert.Equal("fiscal_certificate_metadata", entity.GetTableName());
        Assert.Contains(entity.GetForeignKeys(), fk =>
            fk.PrincipalEntityType.ClrType == typeof(FiscalMemory) &&
            fk.DeleteBehavior == DeleteBehavior.Restrict);
        Assert.Contains(entity.GetIndexes(), i => i.IsUnique &&
            i.GetFilter() == "superseded_at_utc IS NULL");
    }

    [Fact]
    public void Public_view_excludes_custody_reference()
    {
        Assert.Null(typeof(FiscalCertificateView).GetProperty("CustodyReference"));
        Assert.Null(typeof(FiscalCertificateView).GetProperty("PrivateKey"));
    }

    [Fact]
    public void Expired_and_revoked_certificates_are_never_reported_as_valid()
    {
        var now = DateTime.UtcNow;
        Assert.Equal(CertificateStatus.Expired, CertificateCustodyPolicy.EffectiveStatus(
            CertificateStatus.Valid, null, now.AddDays(-1), now));
        Assert.Equal(CertificateStatus.Revoked, CertificateCustodyPolicy.EffectiveStatus(
            CertificateStatus.Revoked, null, now.AddDays(-1), now));
    }
}
