using System.Text;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Customers;

public enum CustomerProfileDecision { Success, Unauthenticated, Forbidden, Invalid, NotFound, Conflict }
public sealed record CustomerProfileUpdate(string? DisplayName, CustomerAccountProfileStatus Status,
    Guid? PreferredPartyId, long ExpectedRevision);
public sealed record CustomerProfileView(Guid CustomerUserId, string DisplayName,
    CustomerAccountProfileStatus Status, bool LoginActive, Guid? PreferredPartyId, long Revision);
public sealed record CustomerProfileResult(CustomerProfileDecision Decision, CustomerProfileView? Profile = null);

public sealed class CustomerAccountProfileService(ArginTaxPayerDbContext db, IdentityAuthorizationService identity)
{
    private Task<bool> IsAdminOfAsync(Guid actor, Guid customer, CancellationToken ct) =>
        db.IdentityUsers.AsNoTracking().AnyAsync(u => u.Id == actor && u.IsActive &&
            db.IdentityMfaStates.Any(m => m.UserId == u.Id && m.EnabledAtUtc != null) &&
            db.CustomerCommercialOwners.Any(o => o.CustomerUserId == customer &&
                (u.Roles.Any(r => r.Role == IdentityRoleNames.Level1) && o.RootLevel1UserId == actor ||
                 u.Roles.Any(r => r.Role == IdentityRoleNames.Level2) && o.PartnerLevel2UserId == actor)), ct);

    private Task<bool> IsCustomerAsync(Guid customer, CancellationToken ct) =>
        db.IdentityUsers.AsNoTracking().AnyAsync(u => u.Id == customer &&
            u.Roles.Any(r => r.Role == IdentityRoleNames.Customer), ct);

    private async Task<CustomerProfileView> ViewAsync(CustomerAccountProfile p, CancellationToken ct)
    {
        var active = await db.IdentityUsers.AsNoTracking().Where(u => u.Id == p.CustomerUserId)
            .Select(u => u.IsActive).SingleAsync(ct);
        return new(p.CustomerUserId, p.DisplayName, p.Status, active, p.PreferredPartyId, p.Revision);
    }

    public async Task<CustomerProfileResult> GetAsync(string? token, Guid customer, CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(token, ct);
        if (actor is null) return new(CustomerProfileDecision.Unauthenticated);
        if (customer == Guid.Empty) return new(CustomerProfileDecision.Invalid);
        if (actor.Value == customer)
        {
            if (!await IsCustomerAsync(customer, ct)) return new(CustomerProfileDecision.Forbidden);
        }
        else if (!await IsAdminOfAsync(actor.Value, customer, ct))
            return new(CustomerProfileDecision.Forbidden);
        var item = await db.CustomerAccountProfiles.AsNoTracking()
            .SingleOrDefaultAsync(p => p.CustomerUserId == customer, ct);
        return item is null ? new(CustomerProfileDecision.NotFound) :
            new(CustomerProfileDecision.Success, await ViewAsync(item, ct));
    }

    public async Task<CustomerProfileResult> SaveAsync(string? token, Guid customer,
        CustomerProfileUpdate? input, CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(token, ct);
        if (actor is null) return new(CustomerProfileDecision.Unauthenticated);
        if (customer == Guid.Empty || input is null || input.DisplayName is null ||
            input.DisplayName.Length > 512 || input.DisplayName.Any(char.IsControl) ||
            !Enum.IsDefined(input.Status) || input.ExpectedRevision < 0 ||
            input.PreferredPartyId == Guid.Empty)
            return new(CustomerProfileDecision.Invalid);
        var name = input.DisplayName.Trim().Normalize(NormalizationForm.FormC);
        if (name.Length is < 2 or > 256) return new(CustomerProfileDecision.Invalid);
        if (!await IsAdminOfAsync(actor.Value, customer, ct))
            return new(CustomerProfileDecision.Forbidden);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, ct);
            if (!await IsAdminOfAsync(actor.Value, customer, ct))
                return new(CustomerProfileDecision.Forbidden);
            if (!await IsCustomerAsync(customer, ct))
                return new(CustomerProfileDecision.NotFound);
            if (input.PreferredPartyId is Guid preferred &&
                !await db.CustomerPartyLinks.AsNoTracking().AnyAsync(l =>
                    l.CustomerUserId == customer && l.PartyId == preferred &&
                    l.UnlinkedAtUtc == null &&
                    db.Parties.Any(p => p.Id == preferred && p.Status == Parties.PartyStatus.Active), ct))
                return new(CustomerProfileDecision.Invalid);
            var item = await db.CustomerAccountProfiles.SingleOrDefaultAsync(p =>
                p.CustomerUserId == customer, ct);
            if (item is null)
            {
                if (input.ExpectedRevision != 0) return new(CustomerProfileDecision.Conflict);
                item = new CustomerAccountProfile { CustomerUserId = customer,
                    CreatedAtUtc = DateTime.UtcNow };
                db.CustomerAccountProfiles.Add(item);
            }
            else
            {
                if (item.Revision != input.ExpectedRevision) return new(CustomerProfileDecision.Conflict);
                item.Revision++;
            }
            item.DisplayName = name;
            item.Status = input.Status;
            item.PreferredPartyId = input.PreferredPartyId;
            item.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new(CustomerProfileDecision.Success, await ViewAsync(item, ct));
        }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return new(CustomerProfileDecision.Conflict); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation })
        { db.ChangeTracker.Clear(); return new(CustomerProfileDecision.Conflict); }
    }
}
