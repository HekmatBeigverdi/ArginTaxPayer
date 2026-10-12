using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Identity;

/// <summary>
/// Opaque, database-backed bearer credentials. No client-controlled roles or tenant IDs.
/// All renewal/revocation decisions are enforced in PostgreSQL.
/// </summary>
public sealed class IdentitySessionService(ArginTaxPayerDbContext db)
{
    private static readonly TimeSpan AccessLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(7);

    public async Task<SessionTokens?> LoginAsync(string? email, string? password, CancellationToken ct = default)
    {
        if (password is null || password.Length > 1024 || password.Length == 0)
            return null;
        string normalized;
        try { normalized = InitialAdministratorBootstrap.NormalizeEmail(email ?? ""); }
        catch (ArgumentException) { return null; }

        var user = await db.IdentityUsers.AsNoTracking()
            .Where(x => x.NormalizedEmail == normalized && x.IsActive)
            .Select(x => new { x.Id, x.PasswordHash })
            .SingleOrDefaultAsync(ct);
        if (user is null || !IdentityPasswordHasher.Verify(password, user.PasswordHash))
            return null;

        // MFA-enabled Customer accounts must not bypass their second factor.
        if (await db.IdentityMfaStates.AsNoTracking().AnyAsync(
            x => x.UserId == user.Id && x.EnabledAtUtc != null, ct)) return null;

        // Administrative identities always require a completed MFA challenge.
        if (await db.IdentityUserRoles.AsNoTracking().AnyAsync(
                x => x.UserId == user.Id && x.Role != IdentityRoleNames.Customer, ct))
            return null;

        var now = DateTime.UtcNow;
        var access = NewToken();
        var refresh = NewToken();
        db.IdentitySessions.Add(new IdentitySession
        {
            Id = Guid.NewGuid(), UserId = user.Id,
            AccessTokenHash = Digest(access), RefreshTokenHash = Digest(refresh),
            CreatedAtUtc = now, AccessExpiresAtUtc = now.Add(AccessLifetime),
            RefreshExpiresAtUtc = now.Add(RefreshLifetime)
        });
        await db.SaveChangesAsync(ct);
        return new SessionTokens(access, refresh, now.Add(AccessLifetime), now.Add(RefreshLifetime));
    }

    internal async Task<SessionTokens?> CreateMfaSessionAsync(Guid userId, CancellationToken ct = default)
    {
        if (!await db.IdentityUsers.AsNoTracking().AnyAsync(x => x.Id == userId && x.IsActive, ct)
            || !await db.IdentityMfaStates.AsNoTracking().AnyAsync(
                x => x.UserId == userId && x.EnabledAtUtc != null, ct))
            return null;
        var now = DateTime.UtcNow;
        var access = NewToken();
        var refresh = NewToken();
        db.IdentitySessions.Add(new IdentitySession
        {
            Id = Guid.NewGuid(), UserId = userId,
            AccessTokenHash = Digest(access), RefreshTokenHash = Digest(refresh),
            CreatedAtUtc = now, AccessExpiresAtUtc = now.Add(AccessLifetime),
            RefreshExpiresAtUtc = now.Add(RefreshLifetime)
        });
        await db.SaveChangesAsync(ct);
        return new SessionTokens(access, refresh, now.Add(AccessLifetime), now.Add(RefreshLifetime));
    }

    public async Task<SessionTokens?> RefreshAsync(string? presentedToken, CancellationToken ct = default)
    {
        if (!ValidToken(presentedToken)) return null;
        var digest = Digest(presentedToken!);
        var now = DateTime.UtcNow;
        var session = await db.IdentitySessions.AsNoTracking()
            .Where(x => x.RefreshTokenHash == digest || x.PreviousRefreshTokenHash == digest)
            .Select(x => new { x.Id, x.RefreshTokenHash, x.PreviousRefreshTokenHash })
            .SingleOrDefaultAsync(ct);
        if (session is null) return null;

        if (session.PreviousRefreshTokenHash == digest)
        {
            // Replay of the immediately previous refresh credential revokes this session.
            await db.IdentitySessions.Where(x => x.Id == session.Id && x.PreviousRefreshTokenHash == digest)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, x => x.RevokedAtUtc ?? now), ct);
            return null;
        }

        var access = NewToken();
        var refresh = NewToken();
        var newAccessDigest = Digest(access);
        var newRefreshDigest = Digest(refresh);
        // Compare-and-swap: only one concurrent refresh can succeed.
        var updated = await db.IdentitySessions
            .Where(x => x.Id == session.Id && x.RefreshTokenHash == digest &&
                x.RevokedAtUtc == null && x.RefreshExpiresAtUtc > now && x.User.IsActive &&
                (!x.User.Roles.Any(r => r.Role != IdentityRoleNames.Customer) ||
                    db.IdentityMfaStates.Any(m => m.UserId == x.UserId && m.EnabledAtUtc != null)))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.AccessTokenHash, newAccessDigest)
                .SetProperty(x => x.PreviousRefreshTokenHash, digest)
                .SetProperty(x => x.RefreshTokenHash, newRefreshDigest)
                .SetProperty(x => x.AccessExpiresAtUtc, now.Add(AccessLifetime))
                .SetProperty(x => x.RefreshExpiresAtUtc, now.Add(RefreshLifetime))
                .SetProperty(x => x.RotationCount, x => x.RotationCount + 1), ct);
        if (updated != 1) return null;
        return new SessionTokens(access, refresh, now.Add(AccessLifetime), now.Add(RefreshLifetime));
    }

    public async Task<Guid?> AuthenticateAsync(string? accessToken, CancellationToken ct = default)
    {
        if (!ValidToken(accessToken)) return null;
        var digest = Digest(accessToken!);
        var now = DateTime.UtcNow;
        return await db.IdentitySessions.AsNoTracking()
            .Where(x => x.AccessTokenHash == digest && x.AccessExpiresAtUtc > now &&
                x.RefreshExpiresAtUtc > now && x.RevokedAtUtc == null && x.User.IsActive &&
                (!x.User.Roles.Any(r => r.Role != IdentityRoleNames.Customer) ||
                    db.IdentityMfaStates.Any(m => m.UserId == x.UserId && m.EnabledAtUtc != null)))
            .Select(x => (Guid?)x.UserId).SingleOrDefaultAsync(ct);
    }

    public async Task<bool> LogoutAsync(string? accessToken, CancellationToken ct = default)
    {
        if (!ValidToken(accessToken)) return false;
        var digest = Digest(accessToken!);
        var now = DateTime.UtcNow;
        return await db.IdentitySessions
            .Where(x => x.AccessTokenHash == digest && x.RevokedAtUtc == null &&
                x.AccessExpiresAtUtc > now)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, now), ct) == 1;
    }

    private static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static bool ValidToken(string? value) =>
        value is { Length: 43 } && value.All(c =>
            c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_');

    private static string Digest(string raw) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(raw))).ToLowerInvariant();
}

public sealed record SessionTokens(
    string AccessToken, string RefreshToken,
    DateTime AccessExpiresAtUtc, DateTime RefreshExpiresAtUtc);
