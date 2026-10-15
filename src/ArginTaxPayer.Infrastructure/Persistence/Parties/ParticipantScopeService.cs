using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Parties;

/// <summary>
/// Buyer and seller registration scope is independent from subscription/invoice emission rights.
/// An active customer may manage its own fiscal participants; an enrolled Level2 its assigned
/// customers; an enrolled Level1 the customers whose root owner matches the administrator.
/// Never treat UI-provided customer IDs or partner roles as sufficient authorization.
/// </summary>
public sealed class ParticipantScopeService(ArginTaxPayerDbContext db)
{
    public Task<bool> CanManageAsync(Guid actor, Guid customer, CancellationToken ct = default) =>
        customer != Guid.Empty && db.IdentityUsers.AsNoTracking().AnyAsync(u =>
            u.Id == actor && u.IsActive &&
            db.IdentityUsers.Any(target => target.Id == customer && target.IsActive &&
                target.Roles.Any(r => r.Role == IdentityRoleNames.Customer)) &&
            (
                u.Id == customer && u.Roles.Any(r => r.Role == IdentityRoleNames.Customer) ||
                db.IdentityMfaStates.Any(m => m.UserId == actor && m.EnabledAtUtc != null) &&
                db.CustomerCommercialOwners.Any(o => o.CustomerUserId == customer &&
                    (u.Roles.Any(r => r.Role == IdentityRoleNames.Level1) &&
                        o.RootLevel1UserId == actor ||
                     u.Roles.Any(r => r.Role == IdentityRoleNames.Level2) &&
                        o.PartnerLevel2UserId == actor))
            ), ct);
}
