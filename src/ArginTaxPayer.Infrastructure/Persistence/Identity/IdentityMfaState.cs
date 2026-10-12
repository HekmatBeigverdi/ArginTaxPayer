namespace ArginTaxPayer.Infrastructure.Persistence.Identity;

/// <summary>Secret is AES-GCM encrypted at rest; never serialize this entity to clients.</summary>
public sealed class IdentityMfaState
{
    public Guid UserId { get; set; }
    public IdentityUser User { get; set; } = null!;
    public string EncryptedSecret { get; set; } = string.Empty;
    public DateTime? EnabledAtUtc { get; set; }
    public long LastAcceptedTotpStep { get; set; } = -1;
    public int RecoveryGeneration { get; set; }
}

public sealed class IdentityRecoveryCode
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public IdentityUser User { get; set; } = null!;
    public string CodeHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
}

public sealed class IdentityMfaChallenge
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public IdentityUser User { get; set; } = null!;
    public string ChallengeHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public int AttemptCount { get; set; }
}
