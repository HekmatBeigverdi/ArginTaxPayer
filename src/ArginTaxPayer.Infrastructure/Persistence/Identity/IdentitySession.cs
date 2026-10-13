namespace ArginTaxPayer.Infrastructure.Persistence.Identity;

/// <summary>Persist only SHA-256 token digests. Raw access/refresh tokens are returned once.</summary>
public sealed class IdentitySession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public IdentityUser User { get; set; } = null!;
    public string AccessTokenHash { get; set; } = string.Empty;
    public string RefreshTokenHash { get; set; } = string.Empty;
    public string? PreviousRefreshTokenHash { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime AccessExpiresAtUtc { get; set; }
    public DateTime RefreshExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public int RotationCount { get; set; }
}
