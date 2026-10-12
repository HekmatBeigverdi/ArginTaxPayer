using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ArginTaxPayer.Infrastructure.Persistence.Identity;

/// <summary>
/// Per-process test/development login throttling. Production needs distributed persistence
/// (Step 10 hardening). Buckets contain HMAC digests, never raw identifiers.
/// </summary>
public sealed class IdentityProtectionService(ILogger<IdentityProtectionService> logger)
{
    private sealed class Bucket
    {
        public readonly object Gate = new();
        public int Failures;
        public DateTime ResetAtUtc;
        public DateTime LockedUntilUtc;
    }

    private readonly ConcurrentDictionary<string, Bucket> buckets = new();
    private readonly byte[] key = RandomNumberGenerator.GetBytes(32);
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);
    public const int MaxFailures = 5;

    private string BucketKey(string email)
    {
        var normal = email.Trim().ToUpperInvariant();
        return Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(normal)));
    }

    public bool IsBlocked(string? email, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        if (!buckets.TryGetValue(BucketKey(email), out var bucket)) return false;
        lock (bucket.Gate) return bucket.LockedUntilUtc > now;
    }

    public void RecordFailure(string? email, DateTime now, string operation)
    {
        if (string.IsNullOrWhiteSpace(email)) return;
        var bucket = buckets.GetOrAdd(BucketKey(email), _ => new Bucket());
        bool blocked;
        lock (bucket.Gate)
        {
            if (bucket.ResetAtUtc <= now)
            {
                bucket.Failures = 0;
                bucket.ResetAtUtc = now.Add(Window);
                bucket.LockedUntilUtc = DateTime.MinValue;
            }
            bucket.Failures++;
            if (bucket.Failures >= MaxFailures) bucket.LockedUntilUtc = now.Add(LockDuration);
            blocked = bucket.LockedUntilUtc > now;
        }
        // Avoid logging email, credential, raw bearer, secret or recovery codes.
        logger.LogWarning("identity.security operation={Operation} outcome={Outcome}",
            operation, blocked ? "throttled" : "rejected");
    }

    public void RecordSuccess(string? email, string operation)
    {
        if (!string.IsNullOrWhiteSpace(email)) buckets.TryRemove(BucketKey(email), out _);
        logger.LogInformation("identity.security operation={Operation} outcome=accepted", operation);
    }
}
