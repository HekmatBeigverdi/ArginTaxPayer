using System.Data;
using ArginTaxPayer.Domain.Parties;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Parties;

public enum PartyIdentityEditStatus { Success, Unauthenticated, Forbidden, Invalid, NotFound, Conflict }
public sealed record PartyIdentityEditInput(int Kind, string? Identifier, string? Name,
    string? LastName, string? RegistrationNumber, long ExpectedRevision);
public sealed record PartyIdentityEditResult(PartyIdentityEditStatus Status, Party? Party = null);

/// <summary>
/// Changes all three master identity fields with one atomic versioned operation.
/// Every change stores the PRIOR version in an immutable history table. Invoice-issued
/// snapshots must be captured by the invoice subsystem when issuing/transmitting invoices;
/// master history is not a substitute for an issued-invoice snapshot.
/// </summary>
public sealed class PartyIdentityEditService(ArginTaxPayerDbContext db,
    IdentityAuthorizationService identity, ParticipantScopeService scope)
{
    public static PartyIdentityHistory CapturePrevious(Party party, Guid actorId, DateTime at) =>
        new()
        {
            Id = Guid.NewGuid(), PartyId = party.Id, ActorUserId = actorId,
            PriorRevision = party.Revision, PriorKind = party.Kind,
            PriorNormalizedIdentifier = party.NormalizedIdentifier,
            PriorDisplayName = party.DisplayName, PriorFirstName = party.FirstName,
            PriorLastName = party.LastName, PriorRegistrationNumber = party.RegistrationNumber,
            CapturedAtUtc = at
        };

    public async Task<PartyIdentityEditResult> UpdateAsync(string? bearer, Guid customerId,
        Guid partyId, PartyIdentityEditInput input, CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(bearer, ct);
        if (actor is null) return new(PartyIdentityEditStatus.Unauthenticated);
        if (customerId == Guid.Empty || partyId == Guid.Empty || input is null ||
            input.ExpectedRevision < 1 || input.Kind is not (1 or 2))
            return new(PartyIdentityEditStatus.Invalid);
        NaturalPersonProfileData? natural = null;
        LegalEntityProfileData? legal = null;
        if (input.Kind == 1)
        {
            if (!NaturalPersonProfilePolicy.TryValidate(input.Identifier,
                input.Name, input.LastName, out natural))
                return new(PartyIdentityEditStatus.Invalid);
        }
        else if (!LegalEntityProfilePolicy.TryValidate(input.Identifier,
                     input.Name, input.RegistrationNumber, out legal))
            return new(PartyIdentityEditStatus.Invalid);

        if (!await scope.CanManageAsync(actor.Value, customerId, ct))
            return new(PartyIdentityEditStatus.Forbidden);
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable, ct);
                if (!await scope.CanManageAsync(actor.Value, customerId, ct))
                    return new PartyIdentityEditResult(PartyIdentityEditStatus.Forbidden);
                var party = await db.Parties.SingleOrDefaultAsync(p => p.Id == partyId &&
                    db.CustomerPartyLinks.Any(link =>
                        link.PartyId == p.Id && link.CustomerUserId == customerId &&
                        link.UnlinkedAtUtc == null), ct);
                if (party is null) return new PartyIdentityEditResult(PartyIdentityEditStatus.NotFound);
                if (party.Revision != input.ExpectedRevision || party.Status != PartyStatus.Active)
                    return new PartyIdentityEditResult(PartyIdentityEditStatus.Conflict);
                // Do not modify a shared master identity owned by another active account.
                if (await db.CustomerPartyLinks.AnyAsync(link => link.PartyId == partyId &&
                    link.CustomerUserId != customerId && link.UnlinkedAtUtc == null, ct))
                    return new PartyIdentityEditResult(PartyIdentityEditStatus.Conflict);

                var newKind = (PartyKind)input.Kind;
                var newIdentifier = input.Kind == 1
                    ? natural!.NormalizedNationalCode : legal!.NormalizedNationalIdentifier;
                var newName = input.Kind == 1 ? natural!.DisplayName : legal!.RegisteredName;
                var newFirst = input.Kind == 1 ? natural!.FirstName : null;
                var newLast = input.Kind == 1 ? natural!.LastName : null;
                var newRegistration = input.Kind == 2 ? legal!.RegistrationNumber : null;
                if (party.Kind == newKind && party.NormalizedIdentifier == newIdentifier &&
                    party.DisplayName == newName && party.FirstName == newFirst &&
                    party.LastName == newLast && party.RegistrationNumber == newRegistration)
                    return new PartyIdentityEditResult(PartyIdentityEditStatus.Success, party);

                // IMPORTANT: No invoice-header table exists in Phase05. There is currently no
                // submitted invoice to query. Invoice creation must capture immutable issued
                // values transactionally BEFORE allowing any later edits to master data.
                var now = DateTime.UtcNow;
                db.PartyIdentityHistories.Add(CapturePrevious(party, actor.Value, now));
                party.Kind = newKind;
                party.NormalizedIdentifier = newIdentifier;
                party.DisplayName = newName;
                party.FirstName = newFirst;
                party.LastName = newLast;
                party.RegistrationNumber = newRegistration;
                party.Revision++;
                party.UpdatedAtUtc = now;
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return new PartyIdentityEditResult(PartyIdentityEditStatus.Success, party);
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return new(PartyIdentityEditStatus.Conflict);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation })
        {
            db.ChangeTracker.Clear();
            return new(PartyIdentityEditStatus.Conflict);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            db.ChangeTracker.Clear();
            return new(PartyIdentityEditStatus.Conflict);
        }
    }
}
