using System.Data;
using ArginTaxPayer.Domain.Parties;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Parties;

public enum NaturalPersonDecision { Success, Unauthenticated, Forbidden, Invalid, NotFound, Conflict }
public sealed record NaturalPersonView(Guid Id, Guid CustomerUserId, string NationalCode,
    string FirstName, string LastName, PartyStatus Status, long Revision);
public sealed record NaturalPersonResult(NaturalPersonDecision Decision, NaturalPersonView? Person = null);

/// <summary>
/// Scoped application service, not a public endpoint. Phase05 Step08 publishes guarded APIs.
/// No party may be linked to a customer without checking live commercial ownership.
/// </summary>
public sealed class NaturalPersonService(ArginTaxPayerDbContext db, IdentityAuthorizationService identity, ParticipantScopeService scope)
{
    private async Task<bool> HasScopeAsync(Guid actor, Guid customer, CancellationToken ct)
    {
        var role = await db.IdentityUsers.AsNoTracking().Where(u => u.Id == actor && u.IsActive)
            .Select(u => new
            {
                Root = u.Roles.Any(x => x.Role == IdentityRoleNames.Level1),
                Partner = u.Roles.Any(x => x.Role == IdentityRoleNames.Level2),
                Customer = u.Roles.Any(x => x.Role == IdentityRoleNames.Customer),
                Mfa = db.IdentityMfaStates.Any(m => m.UserId == u.Id && m.EnabledAtUtc != null)
            }).SingleOrDefaultAsync(ct);
        if (role is null) return false;
        if (role.Customer && actor == customer) return true;
        if (!role.Mfa) return false;
        return await db.CustomerCommercialOwners.AsNoTracking().AnyAsync(o =>
            o.CustomerUserId == customer &&
            (role.Root && o.RootLevel1UserId == actor ||
             role.Partner && o.PartnerLevel2UserId == actor), ct);
    }

    private static NaturalPersonView Map(Party person, Guid customer) =>
        new(person.Id, customer, person.NormalizedIdentifier,
            person.FirstName ?? string.Empty, person.LastName ?? string.Empty,
            person.Status, person.Revision);

    public async Task<NaturalPersonResult> CreateAsync(string? bearer, Guid customerId,
        string? nationalCode, string? firstName, string? lastName, CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(bearer, ct);
        if (actor is null) return new(NaturalPersonDecision.Unauthenticated);
        if (customerId == Guid.Empty ||
            !NaturalPersonProfilePolicy.TryValidate(nationalCode, firstName, lastName, out var validated))
            return new(NaturalPersonDecision.Invalid);
        // Customer self-service activation is reserved for the Phase12 entitlement boundary.
        if (!await scope.CanManageAsync(actor.Value, customerId, ct))
            return new(NaturalPersonDecision.Forbidden);
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                if (!await scope.CanManageAsync(actor.Value, customerId, ct))
                    return new NaturalPersonResult(NaturalPersonDecision.Forbidden);
                var now = DateTime.UtcNow;
                var person = new Party
                {
                    Id = Guid.NewGuid(), Kind = PartyKind.NaturalPerson,
                    NormalizedIdentifier = validated!.NormalizedNationalCode,
                    FirstName = validated.FirstName, LastName = validated.LastName,
                    DisplayName = validated.DisplayName, Status = PartyStatus.Active,
                    Revision = 1, CreatedAtUtc = now, UpdatedAtUtc = now
                };
                db.Parties.Add(person);
                db.CustomerPartyLinks.Add(new CustomerPartyLink
                {
                    Id = Guid.NewGuid(), PartyId = person.Id, CustomerUserId = customerId,
                    LinkedAtUtc = now
                });
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new NaturalPersonResult(NaturalPersonDecision.Success, Map(person, customerId));
            });
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation })
        {
            db.ChangeTracker.Clear();
            return new(NaturalPersonDecision.Conflict);
        }
    }

    public async Task<NaturalPersonResult> GetAsync(string? bearer, Guid customerId,
        Guid partyId, CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(bearer, ct);
        if (actor is null) return new(NaturalPersonDecision.Unauthenticated);
        if (!await HasScopeAsync(actor.Value, customerId, ct))
            return new(NaturalPersonDecision.Forbidden);
        var person = await db.Parties.AsNoTracking()
            .Where(p => p.Id == partyId && p.Kind == PartyKind.NaturalPerson &&
                db.CustomerPartyLinks.Any(l => l.PartyId == p.Id &&
                    l.CustomerUserId == customerId && l.UnlinkedAtUtc == null))
            .SingleOrDefaultAsync(ct);
        return person is null ? new(NaturalPersonDecision.NotFound) :
            new(NaturalPersonDecision.Success, Map(person, customerId));
    }

    public async Task<NaturalPersonResult> UpdateAsync(string? bearer, Guid customerId,
        Guid partyId, long expectedRevision, string? nationalCode,
        string? firstName, string? lastName, bool archive, CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(bearer, ct);
        if (actor is null) return new(NaturalPersonDecision.Unauthenticated);
        if (customerId == Guid.Empty || partyId == Guid.Empty || expectedRevision < 1 ||
            !NaturalPersonProfilePolicy.TryValidate(nationalCode, firstName, lastName, out var validated))
            return new(NaturalPersonDecision.Invalid);
        if (!await scope.CanManageAsync(actor.Value, customerId, ct))
            return new(NaturalPersonDecision.Forbidden);
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                if (!await scope.CanManageAsync(actor.Value, customerId, ct))
                    return new NaturalPersonResult(NaturalPersonDecision.Forbidden);
                var person = await db.Parties.SingleOrDefaultAsync(p =>
                    p.Id == partyId && p.Kind == PartyKind.NaturalPerson &&
                    db.CustomerPartyLinks.Any(l => l.CustomerUserId == customerId &&
                        l.PartyId == p.Id && l.UnlinkedAtUtc == null), ct);
                if (person is null) return new NaturalPersonResult(NaturalPersonDecision.NotFound);
                if (person.Revision != expectedRevision || person.Status == PartyStatus.Archived)
                    return new NaturalPersonResult(NaturalPersonDecision.Conflict);
                // Phase06 will define shared-party conflict resolution. Never mutate an
                // economic actor visible to another customer through an unrelated link.
                if (await db.CustomerPartyLinks.AnyAsync(l => l.PartyId == person.Id &&
                    l.CustomerUserId != customerId && l.UnlinkedAtUtc == null, ct))
                    return new NaturalPersonResult(NaturalPersonDecision.Conflict);
                db.PartyIdentityHistories.Add(PartyIdentityEditService.CapturePrevious(
                    person, actor.Value, DateTime.UtcNow));
                person.NormalizedIdentifier = validated!.NormalizedNationalCode;
                person.FirstName = validated.FirstName;
                person.LastName = validated.LastName;
                person.DisplayName = validated.DisplayName;
                person.Status = archive ? PartyStatus.Archived : PartyStatus.Active;
                person.Revision++;
                person.UpdatedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new NaturalPersonResult(NaturalPersonDecision.Success, Map(person, customerId));
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return new(NaturalPersonDecision.Conflict);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            return new(NaturalPersonDecision.Conflict);
        }
    }

}
