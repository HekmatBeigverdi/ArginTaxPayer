using ArginTaxPayer.Domain.Parties;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Parties;

public enum PartyDetailsDecision { Success, Unauthenticated, Forbidden, Invalid, NotFound, Conflict }
public sealed record PartyContactInput(PartyContactKind Kind, string? Value, string? ContactPerson, bool IsPrimary);
public sealed record PartyAddressInput(string? Label, string? CountryCode, string? Province,
    string? City, string? StreetAddress, string? PostalCode, bool IsPrimary);
public sealed record PartyDetailsResult(PartyDetailsDecision Decision, Guid? Id = null, long? Revision = null);

/// <summary>Scoped domain application operations; Step08 will expose authorized HTTP endpoints.</summary>
public sealed class PartyContactAddressService(
    ArginTaxPayerDbContext db, IdentityAuthorizationService identity,
    ParticipantScopeService scope)
{
    private async Task<PartyDetailsDecision?> AuthorizeAsync(string? token,
        Guid customerId, Guid partyId, CancellationToken ct)
    {
        var actor = await identity.CurrentUserIdAsync(token, ct);
        if (actor is null) return PartyDetailsDecision.Unauthenticated;
        if (customerId == Guid.Empty || partyId == Guid.Empty)
            return PartyDetailsDecision.Invalid;
        var permitted = await scope.CanManageAsync(actor.Value, customerId, ct);
        if (!permitted) return PartyDetailsDecision.Forbidden;
        var member = await db.CustomerPartyLinks.AsNoTracking().AnyAsync(l =>
            l.PartyId == partyId && l.CustomerUserId == customerId && l.UnlinkedAtUtc == null &&
            db.Parties.Any(p => p.Id == l.PartyId && p.Status == PartyStatus.Active), ct);
        if (!member) return PartyDetailsDecision.NotFound;
        // A globally shared party must not be changed through just one customer.
        if (await db.CustomerPartyLinks.AsNoTracking().AnyAsync(l =>
            l.PartyId == partyId && l.CustomerUserId != customerId && l.UnlinkedAtUtc == null, ct))
            return PartyDetailsDecision.Conflict;
        return null;
    }

    public async Task<(PartyDetailsDecision Decision, PartyContact[] Contacts, PartyAddress[] Addresses)>
        ListAsync(string? bearer, Guid customerId, Guid partyId, CancellationToken ct = default)
    {
        var denied = await AuthorizeAsync(bearer, customerId, partyId, ct);
        if (denied is not null) return (denied.Value, [], []);
        var contacts = await db.PartyContacts.AsNoTracking()
            .Where(x => x.PartyId == partyId && x.IsActive)
            .OrderByDescending(x => x.IsPrimary).ThenBy(x => x.Id).ToArrayAsync(ct);
        var addresses = await db.PartyAddresses.AsNoTracking()
            .Where(x => x.PartyId == partyId && x.IsActive)
            .OrderByDescending(x => x.IsPrimary).ThenBy(x => x.Id).ToArrayAsync(ct);
        return (PartyDetailsDecision.Success, contacts, addresses);
    }

    public async Task<PartyDetailsResult> SaveContactAsync(string? bearer, Guid customerId,
        Guid partyId, Guid? id, long? expectedRevision, PartyContactInput input,
        CancellationToken ct = default)
    {
        if (input is null || !PartyContactAddressValidation.TryContact((int)input.Kind,
                input.Value, input.ContactPerson, out var value, out var person) ||
            (id.HasValue && (id == Guid.Empty || expectedRevision is null or < 1)) ||
            (!id.HasValue && expectedRevision is not null))
            return new(PartyDetailsDecision.Invalid);
        var denied = await AuthorizeAsync(bearer, customerId, partyId, ct);
        if (denied is not null) return new(denied.Value);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            denied = await AuthorizeAsync(bearer, customerId, partyId, ct);
            if (denied is not null) return new(denied.Value);
            PartyContact contact;
            if (id.HasValue)
            {
                contact = await db.PartyContacts.SingleOrDefaultAsync(x =>
                    x.Id == id.Value && x.PartyId == partyId, ct) ?? new PartyContact();
                if (contact.Id == Guid.Empty) return new(PartyDetailsDecision.NotFound);
                if (!contact.IsActive || contact.Revision != expectedRevision)
                    return new(PartyDetailsDecision.Conflict);
                contact.Revision++;
            }
            else
            {
                contact = new PartyContact
                {
                    Id = Guid.NewGuid(), PartyId = partyId, CreatedAtUtc = DateTime.UtcNow
                };
                db.PartyContacts.Add(contact);
            }
            if (input.IsPrimary)
            {
                var other = await db.PartyContacts.AnyAsync(x => x.PartyId == partyId &&
                    x.Kind == input.Kind && x.IsActive && x.IsPrimary && x.Id != contact.Id, ct);
                if (other) return new(PartyDetailsDecision.Conflict);
            }
            contact.Kind = input.Kind;
            contact.Value = value;
            contact.ContactPerson = person;
            contact.IsPrimary = input.IsPrimary;
            contact.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new(PartyDetailsDecision.Success, contact.Id, contact.Revision);
        }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return new(PartyDetailsDecision.Conflict); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.CheckViolation })
        { db.ChangeTracker.Clear(); return new(PartyDetailsDecision.Conflict); }
    }

    public async Task<PartyDetailsResult> SaveAddressAsync(string? bearer, Guid customerId,
        Guid partyId, Guid? id, long? expectedRevision, PartyAddressInput input,
        CancellationToken ct = default)
    {
        if (input is null || !PartyContactAddressValidation.TryAddress(input.Label, input.CountryCode,
                input.Province, input.City, input.StreetAddress, input.PostalCode, out var normalized) ||
            (id.HasValue && (id == Guid.Empty || expectedRevision is null or < 1)) ||
            (!id.HasValue && expectedRevision is not null))
            return new(PartyDetailsDecision.Invalid);
        var denied = await AuthorizeAsync(bearer, customerId, partyId, ct);
        if (denied is not null) return new(denied.Value);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            denied = await AuthorizeAsync(bearer, customerId, partyId, ct);
            if (denied is not null) return new(denied.Value);
            PartyAddress address;
            if (id.HasValue)
            {
                address = await db.PartyAddresses.SingleOrDefaultAsync(x =>
                    x.Id == id.Value && x.PartyId == partyId, ct) ?? new PartyAddress();
                if (address.Id == Guid.Empty) return new(PartyDetailsDecision.NotFound);
                if (!address.IsActive || address.Revision != expectedRevision)
                    return new(PartyDetailsDecision.Conflict);
                address.Revision++;
            }
            else
            {
                address = new PartyAddress
                {
                    Id = Guid.NewGuid(), PartyId = partyId, CreatedAtUtc = DateTime.UtcNow
                };
                db.PartyAddresses.Add(address);
            }
            if (input.IsPrimary && await db.PartyAddresses.AnyAsync(x =>
                x.PartyId == partyId && x.IsActive && x.IsPrimary && x.Id != address.Id, ct))
                return new(PartyDetailsDecision.Conflict);
            address.Label = normalized!.Label;
            address.CountryCode = normalized.CountryCode;
            address.Province = normalized.Province;
            address.City = normalized.City;
            address.StreetAddress = normalized.StreetAddress;
            address.PostalCode = normalized.PostalCode;
            address.IsPrimary = input.IsPrimary;
            address.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new(PartyDetailsDecision.Success, address.Id, address.Revision);
        }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return new(PartyDetailsDecision.Conflict); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.CheckViolation })
        { db.ChangeTracker.Clear(); return new(PartyDetailsDecision.Conflict); }
    }

    public async Task<PartyDetailsResult> DeactivateContactAsync(string? bearer, Guid customerId,
        Guid partyId, Guid id, long expectedRevision, CancellationToken ct = default)
    {
        var denied = await AuthorizeAsync(bearer, customerId, partyId, ct);
        if (denied is not null) return new(denied.Value);
        if (id == Guid.Empty || expectedRevision < 1) return new(PartyDetailsDecision.Invalid);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        denied = await AuthorizeAsync(bearer, customerId, partyId, ct);
        if (denied is not null) return new(denied.Value);
        var item = await db.PartyContacts.SingleOrDefaultAsync(x => x.Id == id && x.PartyId == partyId, ct);
        if (item is null) return new(PartyDetailsDecision.NotFound);
        if (!item.IsActive || item.Revision != expectedRevision) return new(PartyDetailsDecision.Conflict);
        item.IsActive = false; item.IsPrimary = false; item.Revision++;
        item.DeactivatedAtUtc = DateTime.UtcNow; item.UpdatedAtUtc = item.DeactivatedAtUtc.Value;
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return new(PartyDetailsDecision.Conflict); }
        return new(PartyDetailsDecision.Success, id, item.Revision);
    }

    public async Task<PartyDetailsResult> DeactivateAddressAsync(string? bearer, Guid customerId,
        Guid partyId, Guid id, long expectedRevision, CancellationToken ct = default)
    {
        var denied = await AuthorizeAsync(bearer, customerId, partyId, ct);
        if (denied is not null) return new(denied.Value);
        if (id == Guid.Empty || expectedRevision < 1) return new(PartyDetailsDecision.Invalid);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        denied = await AuthorizeAsync(bearer, customerId, partyId, ct);
        if (denied is not null) return new(denied.Value);
        var item = await db.PartyAddresses.SingleOrDefaultAsync(x => x.Id == id && x.PartyId == partyId, ct);
        if (item is null) return new(PartyDetailsDecision.NotFound);
        if (!item.IsActive || item.Revision != expectedRevision) return new(PartyDetailsDecision.Conflict);
        item.IsActive = false; item.IsPrimary = false; item.Revision++;
        item.DeactivatedAtUtc = DateTime.UtcNow; item.UpdatedAtUtc = item.DeactivatedAtUtc.Value;
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return new(PartyDetailsDecision.Conflict); }
        return new(PartyDetailsDecision.Success, id, item.Revision);
    }
}
