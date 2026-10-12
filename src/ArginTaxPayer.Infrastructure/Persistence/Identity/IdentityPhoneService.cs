using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ArginTaxPayer.Infrastructure.Persistence.Identity;

/// <summary>
/// Development-only phone OTP lifecycle. A real SMS transport must be explicitly enabled.
/// Store only keyed code digests and metadata; never expose the raw OTP through HTTP.
/// </summary>
public sealed class IdentityPhoneService(ArginTaxPayerDbContext db, IIdentityOtpSender sender,
    IdentitySessionService sessions, IConfiguration configuration)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(60);

    public static string CodeDigest(string pepper, Guid challengeId, string mobile, string purpose, string code)
    {
        var material = Encoding.UTF8.GetBytes(challengeId.ToString("N") + "|" + mobile + "|" + purpose + "|" + code);
        return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(pepper), material));
    }

    private string Pepper() => configuration["Identity:OtpPepper"] is { Length: >= 32 } secret
        ? secret : throw new InvalidOperationException("Identity:OtpPepper must be configured with at least 32 characters.");

    public async Task<bool> RequestAsync(string? mobileInput, string? purpose, CancellationToken ct = default)
    {
        if (!IranianMobileNumber.TryNormalize(mobileInput, out var mobile) ||
            purpose is not ("register" or "login" or "reset")) return false;
        if (!configuration.GetValue<bool>("Ghasedak:EnableLiveSending") &&
            configuration["Identity:OtpDeliveryMode"] != "LocalFile") return false;
        var now = DateTime.UtcNow;

        // Prevent too many sends even across multiple API workers by checking persisted attempts.
        var recent = await db.IdentityPhoneChallenges.AsNoTracking()
            .Where(x => x.Mobile == mobile && x.CreatedAtUtc > now.AddMinutes(-15))
            .OrderByDescending(x => x.CreatedAtUtc).Select(x => x.CreatedAtUtc).Take(6).ToListAsync(ct);
        if (recent.Count >= 5 || recent.Count != 0 && recent[0] > now.Subtract(Cooldown))
            return false;
        var actor = await db.IdentityUsers.AsNoTracking()
            .Where(x => x.NormalizedMobile == mobile && x.MobileVerifiedAtUtc != null && x.IsActive)
            .Select(x => new { x.Id, x.PasswordHash }).SingleOrDefaultAsync(ct);
        if (purpose == "register" && actor is not null) return false;
        if (purpose != "register" && actor is null) return false;
        // OTP login is reserved for passwordless Customer accounts; admin always uses MFA.
        if (purpose == "login" &&
            (actor!.PasswordHash is not null || await db.IdentityUserRoles.AnyAsync(
                x => x.UserId == actor.Id && x.Role != IdentityRoleNames.Customer, ct))) return false;
        if (purpose == "reset" && await db.IdentityUserRoles.AnyAsync(
            x => x.UserId == actor!.Id && x.Role != IdentityRoleNames.Customer, ct)) return false;

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var id = Guid.NewGuid();
        db.IdentityPhoneChallenges.Add(new IdentityPhoneChallenge {
            Id = id, Mobile = mobile, Purpose = purpose,
            CodeDigest = CodeDigest(Pepper(), id, mobile, purpose, code),
            UserId = actor?.Id, CreatedAtUtc = now, ExpiresAtUtc = now.Add(Lifetime)
        });
        await db.SaveChangesAsync(ct);
        bool delivered = await sender.SendAsync(mobile, code, id, ct);
        if (delivered)
            await db.IdentityPhoneChallenges.Where(x => x.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Delivered, true), ct);
        return delivered;
    }

    private async Task<IdentityPhoneChallenge?> ConsumeAsync(string? mobileInput, string purpose,
        string? code, CancellationToken ct)
    {
        if (!IranianMobileNumber.TryNormalize(mobileInput, out var mobile) ||
            code is not { Length: 6 } || !code.All(char.IsAsciiDigit)) return null;
        var now = DateTime.UtcNow;
        var candidate = await db.IdentityPhoneChallenges.AsNoTracking()
            .Where(x => x.Mobile == mobile && x.Purpose == purpose && x.Delivered &&
                x.ExpiresAtUtc > now && x.ConsumedAtUtc == null && x.Attempts < 5)
            .OrderByDescending(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);
        if (candidate is null) return null;
        // Atomically count all guesses across API replicas; failed proof is not rolled back.
        int attempts = await db.IdentityPhoneChallenges.Where(x =>
            x.Id == candidate.Id && x.ConsumedAtUtc == null && x.ExpiresAtUtc > now && x.Attempts < 5)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Attempts, x => x.Attempts + 1), ct);
        if (attempts != 1) return null;
        var digest = CodeDigest(Pepper(), candidate.Id, mobile, purpose, code);
        if (!CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(digest), Convert.FromHexString(candidate.CodeDigest))) return null;
        var claimed = await db.IdentityPhoneChallenges.Where(x =>
            x.Id == candidate.Id && x.ConsumedAtUtc == null && x.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ConsumedAtUtc, now), ct);
        return claimed == 1 ? candidate : null;
    }

    public async Task<bool> ConfirmRegistrationAsync(string? mobile, string? code, string? password,
        CancellationToken ct = default)
    {
        if (password is not null)
        {
            try { IdentityPasswordHasher.Validate(password); }
            catch (ArgumentException) { return false; }
        }
        // Consume proof first, independently of account creation, so failed guesses
        // cannot be rolled back by a caller-managed transaction.
        var proof = await ConsumeAsync(mobile, "register", code, ct);
        if (proof is null) return false;
        if (await db.IdentityUsers.AnyAsync(x => x.NormalizedMobile == proof.Mobile, ct)) return false;
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.IdentityUsers.Add(new IdentityUser {
            Id = id, NormalizedMobile = proof.Mobile, MobileVerifiedAtUtc = now,
            PasswordHash = password is null ? null : IdentityPasswordHasher.Hash(password),
            CreatedAtUtc = now, PasswordChangedAtUtc = password is null ? null : now,
            IsActive = true,
            Roles = new List<IdentityUserRole> { new() {
                UserId = id, Role = IdentityRoleNames.Customer, AssignedAtUtc = now
            } }
        });
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateException error) when (error.InnerException is Npgsql.PostgresException
            { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        { db.ChangeTracker.Clear(); return false; }
    }

    public async Task<SessionTokens?> CompleteLoginAsync(string? mobile, string? code,
        CancellationToken ct = default)
    {
        var proof = await ConsumeAsync(mobile, "login", code, ct);
        if (proof?.UserId is not { } userId) return null;
        return await sessions.CreatePhoneSessionAsync(userId, ct);
    }

    public async Task<bool> ResetPasswordAsync(string? mobile, string? code, string? password,
        CancellationToken ct = default)
    {
        try { IdentityPasswordHasher.Validate(password ?? ""); }
        catch (ArgumentException) { return false; }
        var proof = await ConsumeAsync(mobile, "reset", code, ct);
        if (proof?.UserId is not { } userId) return false;
        if (await db.IdentityUserRoles.AnyAsync(
            x => x.UserId == userId && x.Role != IdentityRoleNames.Customer, ct)) return false;
        var now = DateTime.UtcNow;
        var newHash = IdentityPasswordHasher.Hash(password!);
        var updated = await db.IdentityUsers.Where(x =>
            x.Id == userId && x.IsActive && x.NormalizedMobile == proof.Mobile)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.PasswordHash, newHash)
                .SetProperty(x => x.PasswordChangedAtUtc, now), ct);
        if (updated != 1) return false;
        await db.IdentitySessions.Where(x => x.UserId == userId && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, now), ct);
        return true;
    }
}
