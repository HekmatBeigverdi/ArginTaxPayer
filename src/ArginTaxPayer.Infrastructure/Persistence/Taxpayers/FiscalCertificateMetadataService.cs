using ArginTaxPayer.Domain.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Taxpayers;

/// <summary>Safe public projection only: never returns vault references or private material.</summary>
public sealed record FiscalCertificateView(
    Guid Id, Guid FiscalMemoryId, string FingerprintSha256, string? Issuer,
    DateTime? NotBeforeUtc, DateTime? NotAfterUtc, CertificateStatus Status,
    CertificateOrigin Origin, DateTime CreatedAtUtc, DateTime? SupersededAtUtc);

public enum CertificateMetadataReadStatus { Success, Unauthenticated, Forbidden, NotFound }
public sealed record CertificateMetadataReadResult(
    CertificateMetadataReadStatus Status, FiscalCertificateView[]? Certificates = null);

public sealed class FiscalCertificateMetadataService(
    ArginTaxPayerDbContext db, IdentityAuthorizationService authorization)
{
    public async Task<CertificateMetadataReadResult> ListAsync(
        string? bearer, Guid taxpayerId, Guid fiscalMemoryId, CancellationToken ct = default)
    {
        var actorId = await authorization.CurrentUserIdAsync(bearer, ct);
        if (actorId is null) return new(CertificateMetadataReadStatus.Unauthenticated);
        var allowed = await db.IdentityUsers.AsNoTracking().AnyAsync(u =>
            u.Id == actorId.Value && u.IsActive &&
            u.Roles.Any(r => r.Role == IdentityRoleNames.Level1) &&
            db.IdentityMfaStates.Any(m => m.UserId == u.Id && m.EnabledAtUtc != null), ct);
        if (!allowed) return new(CertificateMetadataReadStatus.Forbidden);
        if (!await db.FiscalMemories.AsNoTracking().AnyAsync(m =>
            m.Id == fiscalMemoryId && m.TaxpayerId == taxpayerId, ct))
            return new(CertificateMetadataReadStatus.NotFound);
        var records = await db.FiscalCertificateMetadata.AsNoTracking()
            .Where(c => c.FiscalMemoryId == fiscalMemoryId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new
            {
                c.Id, c.FiscalMemoryId, c.FingerprintSha256, c.Issuer,
                c.NotBeforeUtc, c.NotAfterUtc, c.Status, c.Origin,
                c.CreatedAtUtc, c.SupersededAtUtc
            }).ToArrayAsync(ct);
        var now = DateTime.UtcNow;
        return new(CertificateMetadataReadStatus.Success, records.Select(c =>
            new FiscalCertificateView(c.Id, c.FiscalMemoryId, c.FingerprintSha256,
                c.Issuer, c.NotBeforeUtc, c.NotAfterUtc,
                CertificateCustodyPolicy.EffectiveStatus(c.Status, c.NotBeforeUtc,
                    c.NotAfterUtc, now),
                c.Origin, c.CreatedAtUtc, c.SupersededAtUtc)).ToArray());
    }
}
