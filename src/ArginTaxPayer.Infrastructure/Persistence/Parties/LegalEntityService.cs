using System.Data;
using ArginTaxPayer.Domain.Parties;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Parties;

public enum LegalEntityDecision { Success, Unauthenticated, Forbidden, Invalid, NotFound, Conflict }
public sealed record LegalEntityView(Guid Id, Guid CustomerUserId, string NationalIdentifier,
    string RegisteredName, string? RegistrationNumber, PartyStatus Status, long Revision);
public sealed record LegalEntityResult(LegalEntityDecision Decision, LegalEntityView? Person = null);

/// <summary>
/// Legal entity service, not an HTTP endpoint. Phase05 Step08 publishes scoped APIs.
/// No party may be linked to a customer without checking live commercial ownership.
/// </summary>
public sealed class LegalEntityService(ArginTaxPayerDbContext db, IdentityAuthorizationService identity, ParticipantScopeService scope)
{
    private Task<bool> HasScopeAsync(Guid actor, Guid customer, CancellationToken ct) =>
        scope.CanManageAsync(actor, customer, ct);

    private static LegalEntityView Map(Party person, Guid customer) =>
        new(person.Id, customer, person.NormalizedIdentifier,
            person.DisplayName, person.RegistrationNumber,
            person.Status, person.Revision);

    public async Task<LegalEntityResult> CreateAsync(string? bearer, Guid customerId,
        string? nationalIdentifier, string? registeredName, string? registrationNumber, CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(bearer, ct);
        if (actor is null) return new(LegalEntityDecision.Unauthenticated);
        if (customerId == Guid.Empty ||
            !LegalEntityProfilePolicy.TryValidate(nationalIdentifier, registeredName, registrationNumber, out var validated))
            return new(LegalEntityDecision.Invalid);
        // Customer self-service activation is reserved for the Phase12 entitlement boundary.
        if (!await scope.CanManageAsync(actor.Value, customerId, ct))
            return new(LegalEntityDecision.Forbidden);
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                if (!await scope.CanManageAsync(actor.Value, customerId, ct))
                    return new LegalEntityResult(LegalEntityDecision.Forbidden);
                var now = DateTime.UtcNow;
                var person = new Party
                {
                    Id = Guid.NewGuid(), Kind = PartyKind.LegalEntity,
                    NormalizedIdentifier = validated!.NormalizedNationalIdentifier,
                    RegistrationNumber = validated.RegistrationNumber,
                    DisplayName = validated.RegisteredName, Status = PartyStatus.Active,
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
                return new LegalEntityResult(LegalEntityDecision.Success, Map(person, customerId));
            });
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation })
        {
            db.ChangeTracker.Clear();
            return new(LegalEntityDecision.Conflict);
        }
    }

    public async Task<LegalEntityResult> GetAsync(string? bearer, Guid customerId,
        Guid partyId, CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(bearer, ct);
        if (actor is null) return new(LegalEntityDecision.Unauthenticated);
        if (!await HasScopeAsync(actor.Value, customerId, ct))
            return new(LegalEntityDecision.Forbidden);
        var person = await db.Parties.AsNoTracking()
            .Where(p => p.Id == partyId && p.Kind == PartyKind.LegalEntity &&
                db.CustomerPartyLinks.Any(l => l.PartyId == p.Id &&
                    l.CustomerUserId == customerId && l.UnlinkedAtUtc == null))
            .SingleOrDefaultAsync(ct);
        return person is null ? new(LegalEntityDecision.NotFound) :
            new(LegalEntityDecision.Success, Map(person, customerId));
    }

    public async Task<LegalEntityResult> UpdateAsync(string? bearer, Guid customerId,
        Guid partyId, long expectedRevision, string? nationalIdentifier,
        string? registeredName, string? registrationNumber, bool archive, CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(bearer, ct);
        if (actor is null) return new(LegalEntityDecision.Unauthenticated);
        if (customerId == Guid.Empty || partyId == Guid.Empty || expectedRevision < 1 ||
            !LegalEntityProfilePolicy.TryValidate(nationalIdentifier, registeredName, registrationNumber, out var validated))
            return new(LegalEntityDecision.Invalid);
        if (!await scope.CanManageAsync(actor.Value, customerId, ct))
            return new(LegalEntityDecision.Forbidden);
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                if (!await scope.CanManageAsync(actor.Value, customerId, ct))
                    return new LegalEntityResult(LegalEntityDecision.Forbidden);
                var person = await db.Parties.SingleOrDefaultAsync(p =>
                    p.Id == partyId && p.Kind == PartyKind.LegalEntity &&
                    db.CustomerPartyLinks.Any(l => l.CustomerUserId == customerId &&
                        l.PartyId == p.Id && l.UnlinkedAtUtc == null), ct);
                if (person is null) return new LegalEntityResult(LegalEntityDecision.NotFound);
                if (person.Revision != expectedRevision || person.Status == PartyStatus.Archived)
                    return new LegalEntityResult(LegalEntityDecision.Conflict);
                // Phase06 will define shared-party conflict resolution. Never mutate an
                // economic actor visible to another customer through an unrelated link.
                if (await db.CustomerPartyLinks.AnyAsync(l => l.PartyId == person.Id &&
                    l.CustomerUserId != customerId && l.UnlinkedAtUtc == null, ct))
                    return new LegalEntityResult(LegalEntityDecision.Conflict);
                db.PartyIdentityHistories.Add(PartyIdentityEditService.CapturePrevious(
                    person, actor.Value, DateTime.UtcNow));
                person.NormalizedIdentifier = validated!.NormalizedNationalIdentifier;
                person.RegistrationNumber = validated.RegistrationNumber;
                person.DisplayName = validated.RegisteredName;
                person.Status = archive ? PartyStatus.Archived : PartyStatus.Active;
                person.Revision++;
                person.UpdatedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new LegalEntityResult(LegalEntityDecision.Success, Map(person, customerId));
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return new(LegalEntityDecision.Conflict);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            return new(LegalEntityDecision.Conflict);
        }
    }

}
