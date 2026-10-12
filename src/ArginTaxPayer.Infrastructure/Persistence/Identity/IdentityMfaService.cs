using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Identity;

/// <summary>
/// MFA state machine: enrollment is privileged, challenges are short-lived and one-use.
/// Admin users cannot log in without pre-provisioned, confirmed MFA.
/// </summary>
public sealed class IdentityMfaService(ArginTaxPayerDbContext db, IdentityMfaCrypto crypto,
    IdentitySessionService sessions)
{
    public async Task<MfaEnrollment?> BeginEnrollmentAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.IdentityUsers.AsNoTracking()
            .Where(x => x.Id == userId && x.IsActive)
            .Select(x => new { x.Id, x.NormalizedEmail }).SingleOrDefaultAsync(ct);
        if (user is null) return null;
        var existing = await db.IdentityMfaStates.SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (existing?.EnabledAtUtc is not null) return null;

        var secret = IdentityTotp.NewSecret();
        if (existing is null)
        {
            db.IdentityMfaStates.Add(new IdentityMfaState {
                UserId = userId, EncryptedSecret = crypto.Encrypt(secret), LastAcceptedTotpStep = -1
            });
        }
        else
        {
            existing.EncryptedSecret = crypto.Encrypt(secret);
            existing.LastAcceptedTotpStep = -1;
        }
        await db.SaveChangesAsync(ct);
        // The URI/secret is returned once and must not be logged.
        return new MfaEnrollment(secret, IdentityTotp.Uri(user.NormalizedEmail, secret));
    }

    /// <summary>Complete enrollment: issue hashed one-use recovery codes only after TOTP proof.</summary>
    public async Task<MfaRecoveryCodes?> ConfirmEnrollmentAsync(Guid userId, string? totpCode,
        CancellationToken ct = default)
    {
        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(ct) : null;
        var state = await db.IdentityMfaStates.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == userId && x.EnabledAtUtc == null, ct);
        if (state is null) return null;
        var step = IdentityTotp.MatchStep(crypto.Decrypt(state.EncryptedSecret),
            totpCode, DateTime.UtcNow, state.LastAcceptedTotpStep);
        if (step is null) return null;

        var now = DateTime.UtcNow;
        var claimed = await db.IdentityMfaStates
            .Where(x => x.UserId == userId && x.EnabledAtUtc == null && x.LastAcceptedTotpStep < step.Value)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.EnabledAtUtc, now)
                .SetProperty(x => x.LastAcceptedTotpStep, step.Value)
                .SetProperty(x => x.RecoveryGeneration, x => x.RecoveryGeneration + 1), ct);
        if (claimed != 1) return null;

        var codes = Enumerable.Range(0, 10).Select(_ => NewRandom()).ToArray();
        foreach (var code in codes)
        {
            db.IdentityRecoveryCodes.Add(new IdentityRecoveryCode
            {
                Id = Guid.NewGuid(), UserId = userId, CodeHash = Digest(code),
                CreatedAtUtc = now
            });
        }
        await db.SaveChangesAsync(ct);
        // An existing password-only session must not survive MFA enrollment.
        await db.IdentitySessions.Where(x => x.UserId == userId && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, now), ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return new MfaRecoveryCodes(codes);
    }

    public async Task<MfaLoginChallenge?> BeginLoginAsync(string? email, string? password,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(password) || password.Length > 1024) return null;
        string normalized;
        try { normalized = InitialAdministratorBootstrap.NormalizeEmail(email ?? ""); }
        catch (ArgumentException) { return null; }

        var user = await db.IdentityUsers.AsNoTracking()
            .Where(x => x.NormalizedEmail == normalized && x.IsActive)
            .Select(x => new { x.Id, x.PasswordHash }).SingleOrDefaultAsync(ct);
        if (user is null || !IdentityPasswordHasher.Verify(password, user.PasswordHash))
            return null;
        if (!await db.IdentityMfaStates.AsNoTracking().AnyAsync(
            x => x.UserId == user.Id && x.EnabledAtUtc != null, ct))
            return null;

        var raw = NewRandom();
        var expiry = DateTime.UtcNow.AddMinutes(5);
        db.IdentityMfaChallenges.Add(new IdentityMfaChallenge
        {
            Id = Guid.NewGuid(), UserId = user.Id, ChallengeHash = Digest(raw),
            ExpiresAtUtc = expiry
        });
        await db.SaveChangesAsync(ct);
        return new MfaLoginChallenge(raw, expiry);
    }

    public async Task<SessionTokens?> CompleteLoginAsync(string? challenge, string? code,
        bool useRecoveryCode, CancellationToken ct = default)
    {
        if (!ValidRaw(challenge) || string.IsNullOrWhiteSpace(code)) return null;

        // The transaction serializes concurrent attempts. Failed attempts are persisted
        // separately before verification, limiting guessing to five per challenge.
        var digest = Digest(challenge!);
        var now = DateTime.UtcNow;
        var updated = await db.IdentityMfaChallenges
            .Where(x => x.ChallengeHash == digest && x.ConsumedAtUtc == null &&
                x.ExpiresAtUtc > now && x.AttemptCount < 5 && x.User.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1), ct);
        if (updated != 1) return null;

        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var tx = ownsTransaction ? await db.Database.BeginTransactionAsync(ct) : null;
        var candidate = await db.IdentityMfaChallenges.AsNoTracking()
            .Where(x => x.ChallengeHash == digest && x.ConsumedAtUtc == null &&
                x.ExpiresAtUtc > now && x.AttemptCount <= 5)
            .Select(x => new { x.Id, x.UserId }).SingleOrDefaultAsync(ct);
        if (candidate is null) return null;

        var state = await db.IdentityMfaStates.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == candidate.UserId && x.EnabledAtUtc != null, ct);
        if (state is null) return null;

        if (useRecoveryCode)
        {
            if (!ValidRaw(code)) return null;
            var hash = Digest(code);
            var consumed = await db.IdentityRecoveryCodes
                .Where(x => x.UserId == candidate.UserId && x.CodeHash == hash && x.UsedAtUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.UsedAtUtc, now), ct);
            if (consumed != 1) return null;
        }
        else
        {
            var step = IdentityTotp.MatchStep(crypto.Decrypt(state.EncryptedSecret), code,
                now, state.LastAcceptedTotpStep);
            if (step is null) return null;
            var used = await db.IdentityMfaStates
                .Where(x => x.UserId == candidate.UserId && x.EnabledAtUtc != null &&
                    x.LastAcceptedTotpStep < step.Value)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastAcceptedTotpStep, step.Value), ct);
            if (used != 1) return null;
        }

        var claimed = await db.IdentityMfaChallenges
            .Where(x => x.Id == candidate.Id && x.ConsumedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ConsumedAtUtc, now), ct);
        if (claimed != 1) return null;

        var tokens = await sessions.CreateMfaSessionAsync(candidate.UserId, ct);
        if (tokens is null) return null;
        if (tx is not null) await tx.CommitAsync(ct);
        return tokens;
    }

    private static string NewRandom() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
        .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static bool ValidRaw(string? token) => token is { Length: 32 } &&
        token.All(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z'
            or >= '0' and <= '9' or '-' or '_');

    private static string Digest(string raw) => Convert.ToHexString(
        SHA256.HashData(Encoding.ASCII.GetBytes(raw))).ToLowerInvariant();
}

public sealed record MfaEnrollment(string Secret, string ProvisioningUri);
public sealed record MfaRecoveryCodes(string[] Codes);
public sealed record MfaLoginChallenge(string Challenge, DateTime ExpiresAtUtc);
