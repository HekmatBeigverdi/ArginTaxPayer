using ArginTaxPayer.Domain.Subscriptions;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

/// <summary>Authenticated plan creation. Plans are globally coded with a server-derived
/// admin prefix, preventing one administrator from impersonating another's catalog.</summary>
public sealed class SubscriptionPlanAdministrationService(
    ArginTaxPayerDbContext db, IdentityAuthorizationService identity,
    SubscriptionCatalogRepository catalog)
{
    public async Task<StoredSubscriptionPlan> CreateAsync(string? accessToken, string code,
        SubscriptionPlanKind kind, int validityDays, long? invoiceUnits,
        CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(accessToken, ct)
            ?? throw new UnauthorizedAccessException("Authentication required.");
        var isAdmin = await db.IdentityUsers.AsNoTracking().AnyAsync(u => u.Id == actor &&
            u.IsActive && (u.Roles.Any(r => r.Role == IdentityRoleNames.Level1) ||
                u.Roles.Any(r => r.Role == IdentityRoleNames.Level2)) &&
            db.IdentityMfaStates.Any(m => m.UserId == actor && m.EnabledAtUtc != null), ct);
        if (!isAdmin) throw new UnauthorizedAccessException("Active administrator with MFA required.");
        if (code is null || code.Length is < 1 or > 50 ||
            !System.Text.RegularExpressions.Regex.IsMatch(code, "^[A-Za-z0-9_-]+$"))
            throw new ArgumentException("Invalid plan code.");
        new SubscriptionPlanTerms(Guid.NewGuid(), kind, validityDays, invoiceUnits).Validate();
        var scopedCode = $"{actor:N}-{code}";
        return await catalog.AddPlanAsync(scopedCode, kind, validityDays, invoiceUnits, ct);
    }
}
