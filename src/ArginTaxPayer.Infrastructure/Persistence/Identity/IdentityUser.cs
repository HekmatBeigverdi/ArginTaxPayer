namespace ArginTaxPayer.Infrastructure.Persistence.Identity;

/// <summary>Global user identity; taxpayer membership is introduced in Phase 04.</summary>
public sealed class IdentityUser
{
    public Guid Id { get; set; }
    public string? NormalizedEmail { get; set; }
    public string? NormalizedMobile { get; set; }
    public DateTime? MobileVerifiedAtUtc { get; set; }
    public string? PasswordHash { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? PasswordChangedAtUtc { get; set; }
    public ICollection<IdentityUserRole> Roles { get; set; } = new List<IdentityUserRole>();
}

/// <summary>Role assignment requires a privileged server-side workflow; it is never client-controlled.</summary>
public sealed class IdentityUserRole
{
    public Guid UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime AssignedAtUtc { get; set; }
    public IdentityUser User { get; set; } = null!;
}

public static class IdentityRoleNames
{
    public const string Level1 = "Level1";
    public const string Level2 = "Level2";
    public const string Customer = "Customer";
    // Invitations carry no administrative permissions. Confirmation activates the role.
    public const string PendingLevel1 = "PendingLevel1";
    public const string PendingLevel2 = "PendingLevel2";

    public static bool CanEnrollMfa(string role) => role is Level1 or Level2 or PendingLevel1 or PendingLevel2;
}
