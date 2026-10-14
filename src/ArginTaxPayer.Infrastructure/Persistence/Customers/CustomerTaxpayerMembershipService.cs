using System.Data;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Customers;

public enum MembershipDecision { Success, Unauthenticated, Forbidden, Invalid, NotFound, Conflict }
public sealed record MembershipView(Guid Id, Guid CustomerUserId, Guid TaxpayerId,
    DateTime LinkedAtUtc, DateTime? UnlinkedAtUtc, long Revision);
public sealed record MembershipResult(MembershipDecision Decision, MembershipView? Membership = null);
public sealed record MembershipListResult(MembershipDecision Decision, MembershipView[] Memberships);

/// <summary>
/// Explicit, audited relationship for a customer's own taxpayers only.
/// TaxpayerProfile.CustomerUserId stays authoritative; no taxpayer transfer here.
/// </summary>
public sealed class CustomerTaxpayerMembershipService(
    ArginTaxPayerDbContext db, IdentityAuthorizationService authorization)
{
    private Task<bool> AdminScopeAsync(Guid actor, Guid customer, CancellationToken ct) =>
        db.IdentityUsers.AsNoTracking().AnyAsync(u => u.Id == actor && u.IsActive &&
            db.IdentityMfaStates.Any(m => m.UserId == u.Id && m.EnabledAtUtc != null) &&
            db.CustomerCommercialOwners.Any(o => o.CustomerUserId == customer &&
                (u.Roles.Any(r => r.Role == IdentityRoleNames.Level1) && o.RootLevel1UserId == actor ||
                 u.Roles.Any(r => r.Role == IdentityRoleNames.Level2) && o.PartnerLevel2UserId == actor)), ct);

    private Task<bool> SelfScopeAsync(Guid actor, Guid customer, CancellationToken ct) =>
        db.IdentityUsers.AsNoTracking().AnyAsync(u => u.Id == actor && u.Id == customer &&
            u.IsActive && u.Roles.Any(r => r.Role == IdentityRoleNames.Customer), ct);

    private static MembershipView View(CustomerTaxpayerMembership m) =>
        new(m.Id, m.CustomerUserId, m.TaxpayerId, m.LinkedAtUtc, m.UnlinkedAtUtc, m.Revision);

    public async Task<MembershipListResult> ListAsync(
        string? bearer, Guid customer, bool includeHistory = false, CancellationToken ct = default)
    {
        var actor = await authorization.CurrentUserIdAsync(bearer, ct);
        if (actor is null) return new(MembershipDecision.Unauthenticated, []);
        if (customer == Guid.Empty) return new(MembershipDecision.Invalid, []);
        if (!await AdminScopeAsync(actor.Value, customer, ct) &&
            !await SelfScopeAsync(actor.Value, customer, ct))
            return new(MembershipDecision.Forbidden, []);
        var query = db.CustomerTaxpayerMemberships.AsNoTracking()
            .Where(x => x.CustomerUserId == customer &&
                db.TaxpayerProfiles.Any(t => t.Id == x.TaxpayerId && t.CustomerUserId == customer));
        if (!includeHistory) query = query.Where(x => x.UnlinkedAtUtc == null);
        var items = await query.OrderBy(x => x.LinkedAtUtc).ThenBy(x => x.Id)
            .Select(x => new MembershipView(x.Id, x.CustomerUserId, x.TaxpayerId,
                x.LinkedAtUtc, x.UnlinkedAtUtc, x.Revision)).ToArrayAsync(ct);
        return new(MembershipDecision.Success, items);
    }

    public async Task<MembershipResult> LinkAsync(
        string? bearer, Guid customer, Guid taxpayer, CancellationToken ct = default)
    {
        var actor = await authorization.CurrentUserIdAsync(bearer, ct);
        if (actor is null) return new(MembershipDecision.Unauthenticated);
        if (customer == Guid.Empty || taxpayer == Guid.Empty) return new(MembershipDecision.Invalid);
        if (!await AdminScopeAsync(actor.Value, customer, ct))
            return new(MembershipDecision.Forbidden);
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                if (!await AdminScopeAsync(actor.Value, customer, ct))
                    return new MembershipResult(MembershipDecision.Forbidden);
                var isAssigned = await db.TaxpayerProfiles.AsNoTracking().AnyAsync(t =>
                    t.Id == taxpayer && t.CustomerUserId == customer, ct);
                if (!isAssigned) return new MembershipResult(MembershipDecision.NotFound);
                if (await db.CustomerTaxpayerMemberships.AnyAsync(m => m.CustomerUserId == customer &&
                    m.TaxpayerId == taxpayer && m.UnlinkedAtUtc == null, ct))
                    return new MembershipResult(MembershipDecision.Conflict);
                var row = new CustomerTaxpayerMembership
                {
                    Id = Guid.NewGuid(), CustomerUserId = customer, TaxpayerId = taxpayer,
                    LinkedByUserId = actor.Value, LinkedAtUtc = DateTime.UtcNow, Revision = 1
                };
                db.CustomerTaxpayerMemberships.Add(row);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new MembershipResult(MembershipDecision.Success, View(row));
            });
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation })
        { db.ChangeTracker.Clear(); return new(MembershipDecision.Conflict); }
    }

    public async Task<MembershipResult> UnlinkAsync(
        string? bearer, Guid customer, Guid membershipId, long expectedRevision,
        CancellationToken ct = default)
    {
        var actor = await authorization.CurrentUserIdAsync(bearer, ct);
        if (actor is null) return new(MembershipDecision.Unauthenticated);
        if (customer == Guid.Empty || membershipId == Guid.Empty || expectedRevision < 1)
            return new(MembershipDecision.Invalid);
        if (!await AdminScopeAsync(actor.Value, customer, ct))
            return new(MembershipDecision.Forbidden);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (!await AdminScopeAsync(actor.Value, customer, ct))
                return new MembershipResult(MembershipDecision.Forbidden);
            var item = await db.CustomerTaxpayerMemberships.SingleOrDefaultAsync(x =>
                x.Id == membershipId && x.CustomerUserId == customer &&
                db.TaxpayerProfiles.Any(t => t.Id == x.TaxpayerId && t.CustomerUserId == customer), ct);
            if (item is null) return new MembershipResult(MembershipDecision.NotFound);
            if (item.UnlinkedAtUtc is not null || item.Revision != expectedRevision)
                return new MembershipResult(MembershipDecision.Conflict);
            var now = DateTime.UtcNow;
            item.UnlinkedAtUtc = now;
            item.UnlinkedByUserId = actor.Value;
            item.Revision++;
            try
            {
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new MembershipResult(MembershipDecision.Success, View(item));
            }
            catch (DbUpdateConcurrencyException)
            {
                db.ChangeTracker.Clear();
                return new MembershipResult(MembershipDecision.Conflict);
            }
        });
    }
}
