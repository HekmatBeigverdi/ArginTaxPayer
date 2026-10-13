using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ArginTaxPayer.Infrastructure.Persistence.Identity;

/// <summary>
/// Development-only phone OTP lifecycle. A real SMS transport must be explicitly enabled.
/// Store only keyed code digests and metadata; never expose the raw OTP through HTTP.
/// </summary>
public sealed class IdentityPhoneService(ArginTaxPayerDbContext db, IIdentityOtpSender sender,
    IdentitySessionService sessions, IConfiguration configuration, ILogger<IdentityPhoneService> logger)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(3);

    public static string CodeDigest(string pepper, Guid challengeId, string mobile, string purpose, string code)
    {
        var material = Encoding.UTF8.GetBytes(challengeId.ToString("N") + "|" + mobile + "|" + purpose + "|" + code);
        return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(pepper), material));
    }

    private string Pepper() => configuration["Identity:OtpPepper"] is { Length: >= 32 } secret
        ? secret : throw new InvalidOperationException("Identity:OtpPepper must be configured with at least 32 characters.");

    public async Task<bool> RequestAsync(string? mobileInput, string? purpose, CancellationToken ct = default)
        => (await RequestDetailedAsync(mobileInput, purpose, ct)).Status == PhoneOtpRequestStatus.Sent;

    /// <summary>
    /// Detailed development-only outcome. Do not expose account-existence distinctions
    /// on an unauthenticated public endpoint in Production.
    /// </summary>
    public async Task<PhoneOtpRequestResult> RequestDetailedAsync(
        string? mobileInput, string? purpose, CancellationToken ct = default)
    {
        if (!IranianMobileNumber.TryNormalize(mobileInput, out var mobile))
        {
            logger.LogWarning("OTP_REQUEST_SKIPPED reason=invalid_mobile");
            return new(PhoneOtpRequestStatus.Invalid);
        }
        if (purpose is not ("register" or "login" or "reset"))
        {
            logger.LogWarning("OTP_REQUEST_SKIPPED reason=invalid_purpose");
            return new(PhoneOtpRequestStatus.Invalid);
        }

        // Account checks precede cooldown: a registered user should see a
        // registration conflict, never a misleading OTP/code entry screen.
        var actor = await db.IdentityUsers.AsNoTracking()
            .Where(x => x.NormalizedMobile == mobile)
            .Select(x => new { x.Id, x.IsActive, x.MobileVerifiedAtUtc })
            .SingleOrDefaultAsync(ct);
        if (purpose == "register" && actor is not null)
        {
            logger.LogInformation("OTP_REQUEST_SKIPPED reason=mobile_already_registered");
            return new(PhoneOtpRequestStatus.AlreadyRegistered);
        }
        if (purpose != "register" && (actor is null || !actor.IsActive ||
            actor.MobileVerifiedAtUtc is null))
        {
            logger.LogInformation("OTP_REQUEST_SKIPPED reason=no_verified_account");
            return new(PhoneOtpRequestStatus.Unavailable);
        }
        if ((purpose is "login" or "reset") && await db.IdentityUserRoles.AnyAsync(
            x => x.UserId == actor!.Id && x.Role != IdentityRoleNames.Customer, ct))
        {
            logger.LogInformation("OTP_REQUEST_SKIPPED reason=administrator_mfa_required");
            return new(PhoneOtpRequestStatus.Unavailable);
        }
        if (purpose == "login" && await db.IdentityMfaStates.AsNoTracking().AnyAsync(
            x => x.UserId == actor!.Id && x.EnabledAtUtc != null, ct))
        {
            logger.LogInformation("OTP_REQUEST_SKIPPED reason=mfa_required");
            return new(PhoneOtpRequestStatus.Unavailable);
        }

        if (!configuration.GetValue<bool>("Ghasedak:EnableLiveSending") &&
            configuration["Identity:OtpDeliveryMode"] != "LocalFile")
        {
            logger.LogWarning("OTP_REQUEST_SKIPPED reason=delivery_disabled");
            return new(PhoneOtpRequestStatus.Unavailable);
        }

        var now = DateTime.UtcNow;
        // The 15-minute budget counts all attempts, including failed provider calls.
        // The three-minute resend cooldown only follows a *successful* provider
        // acceptance: a failed dispatch must not misleadingly block retry for 3 min.
        var recent = await db.IdentityPhoneChallenges.AsNoTracking()
            .Where(x => x.Mobile == mobile && x.CreatedAtUtc > now.AddMinutes(-15))
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new { x.CreatedAtUtc, x.Delivered }).Take(5).ToListAsync(ct);
        if (recent.Count >= 5)
        {
            var wait = RemainingSeconds(recent[4].CreatedAtUtc.AddMinutes(15), now);
            logger.LogWarning("OTP_REQUEST_SKIPPED reason=rate_limit_15m remaining_seconds={RemainingSeconds}", wait);
            return new(PhoneOtpRequestStatus.RateLimited, wait);
        }
        var lastDelivered = recent.FirstOrDefault(x => x.Delivered);
        if (lastDelivered is not null && lastDelivered.CreatedAtUtc > now.Subtract(Cooldown))
        {
            var wait = RemainingSeconds(lastDelivered.CreatedAtUtc.Add(Cooldown), now);
            logger.LogWarning("OTP_REQUEST_SKIPPED reason=cooldown_180s remaining_seconds={RemainingSeconds}", wait);
            return new(PhoneOtpRequestStatus.RateLimited, wait);
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var id = Guid.NewGuid();
        db.IdentityPhoneChallenges.Add(new IdentityPhoneChallenge {
            Id = id, Mobile = mobile, Purpose = purpose,
            CodeDigest = CodeDigest(Pepper(), id, mobile, purpose, code),
            UserId = actor?.Id, CreatedAtUtc = now, ExpiresAtUtc = now.Add(Lifetime)
        });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("OTP_REQUEST_DISPATCH_STARTED purpose={OtpPurpose}", purpose);
        var delivered = await sender.SendAsync(mobile, code, purpose, id, ct);
        if (!delivered)
        {
            logger.LogWarning("OTP_REQUEST_DISPATCH_FAILED purpose={OtpPurpose}", purpose);
            return new(PhoneOtpRequestStatus.Unavailable);
        }
        await db.IdentityPhoneChallenges.Where(x => x.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Delivered, true), ct);
        return new(PhoneOtpRequestStatus.Sent);
    }

    private static int RemainingSeconds(DateTime untilUtc, DateTime nowUtc)
        => Math.Max(1, (int)Math.Ceiling((untilUtc - nowUtc).TotalSeconds));

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

public enum PhoneOtpRequestStatus
{
    Sent,
    Invalid,
    AlreadyRegistered,
    RateLimited,
    Unavailable
}

public sealed record PhoneOtpRequestResult(
    PhoneOtpRequestStatus Status, int RetryAfterSeconds = 0);
