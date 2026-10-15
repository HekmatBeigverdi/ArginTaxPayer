using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Identity;
public sealed record SoftwareProfileWrite(string? FirstName, string? LastName, string? CompanyName,
    string? Address, long ExpectedRevision);
public sealed record SoftwareProfileView(Guid UserId, string FirstName, string LastName,
    string? CompanyName, string Address, bool HasLogo, bool Complete, long Revision);
public enum SoftwareProfileStatus { Success, Unauthenticated, Invalid, Conflict, NotFound }
public sealed record SoftwareProfileResult(SoftwareProfileStatus Status, SoftwareProfileView? Data = null);

public sealed class SoftwareUserProfileService(ArginTaxPayerDbContext db, IdentityAuthorizationService identity)
{
    public static bool Complete(SoftwareUserProfile? row) => row is not null &&
        !string.IsNullOrWhiteSpace(row.FirstName) && !string.IsNullOrWhiteSpace(row.LastName) &&
        !string.IsNullOrWhiteSpace(row.Address);
    public async Task<Guid?> ActorAsync(string? token, CancellationToken ct) =>
        await identity.CurrentUserIdAsync(token, ct);

    public async Task<SoftwareProfileResult> GetAsync(string? token, CancellationToken ct)
    {
        var actor = await ActorAsync(token, ct);
        if (actor is null) return new(SoftwareProfileStatus.Unauthenticated);
        var p = await db.SoftwareUserProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == actor, ct);
        return new(SoftwareProfileStatus.Success, p is null ?
            new(actor.Value, "", "", null, "", false, false, 0) :
            new(actor.Value, p.FirstName, p.LastName, p.CompanyName, p.Address,
                p.InvoiceLogoPng != null, Complete(p), p.Revision));
    }

    private static string? Clean(string? value, int max, bool required)
    {
        if (value is null || value.Length > max * 2 || value.Any(char.IsControl)) return null;
        var trimmed = value.Trim().Normalize(NormalizationForm.FormC);
        if (trimmed.Length > max || required && trimmed.Length < 2) return null;
        return trimmed;
    }

    public async Task<SoftwareProfileResult> SaveAsync(string? token, SoftwareProfileWrite? input,
        CancellationToken ct)
    {
        var actor = await ActorAsync(token, ct);
        if (actor is null) return new(SoftwareProfileStatus.Unauthenticated);
        if (input is null || input.ExpectedRevision < 0) return new(SoftwareProfileStatus.Invalid);
        var first = Clean(input.FirstName, 100, true);
        var last = Clean(input.LastName, 100, true);
        var company = input.CompanyName is null ? "" : Clean(input.CompanyName, 256, false);
        var address = Clean(input.Address, 500, true);
        if (first is null || last is null || company is null || address is null)
            return new(SoftwareProfileStatus.Invalid);
        try
        {
            var row = await db.SoftwareUserProfiles.SingleOrDefaultAsync(x => x.UserId == actor, ct);
            if (row is null)
            {
                if (input.ExpectedRevision != 0) return new(SoftwareProfileStatus.Conflict);
                row = new SoftwareUserProfile { UserId = actor.Value, CompletedAtUtc = DateTime.UtcNow };
                db.SoftwareUserProfiles.Add(row);
            }
            else
            {
                if (row.Revision != input.ExpectedRevision) return new(SoftwareProfileStatus.Conflict);
                row.Revision++;
            }
            row.FirstName = first; row.LastName = last;
            row.CompanyName = company.Length == 0 ? null : company;
            row.Address = address; row.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return new(SoftwareProfileStatus.Success,
                new(row.UserId, row.FirstName, row.LastName, row.CompanyName, row.Address,
                    row.InvoiceLogoPng is not null, true, row.Revision));
        }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return new(SoftwareProfileStatus.Conflict); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation }) {
            db.ChangeTracker.Clear(); return new(SoftwareProfileStatus.Conflict);
        }
    }

    // Store a normalized 512x512 PNG only; server verifies dimensions and signature
    // instead of trusting MIME, file extension or the browser's resize claim.
    public async Task<SoftwareProfileStatus> SaveLogoAsync(string? token, byte[] png, CancellationToken ct)
    {
        var actor = await ActorAsync(token, ct);
        if (actor is null) return SoftwareProfileStatus.Unauthenticated;
        if (png.Length is < 45 or > 350000 ||
            !png.AsSpan(0, 8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}) ||
            !png.AsSpan(12, 4).SequenceEqual("IHDR"u8) ||
            System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)) != 512 ||
            System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)) != 512 ||
            png[24] != 8 || png[25] is not (2 or 6))
            return SoftwareProfileStatus.Invalid;
        var row = await db.SoftwareUserProfiles.SingleOrDefaultAsync(x => x.UserId == actor, ct);
        if (!Complete(row)) return SoftwareProfileStatus.Invalid;
        row!.InvoiceLogoPng = png.ToArray();
        row.Revision++; row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return SoftwareProfileStatus.Success;
    }
    public async Task<(SoftwareProfileStatus, byte[]?)> ReadLogoAsync(string? token, CancellationToken ct)
    {
        var actor = await ActorAsync(token, ct);
        if (actor is null) return (SoftwareProfileStatus.Unauthenticated, null);
        var logo = await db.SoftwareUserProfiles.AsNoTracking().Where(x => x.UserId == actor)
            .Select(x => x.InvoiceLogoPng).SingleOrDefaultAsync(ct);
        return logo is null ? (SoftwareProfileStatus.NotFound, null) :
            (SoftwareProfileStatus.Success, logo);
    }
}
