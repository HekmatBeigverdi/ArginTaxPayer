using System.Data;
using System.Text;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Parties;

public enum InvoiceParticipantOutcome { Success, Unauthenticated, Forbidden, Invalid, NotFound, Conflict }
public sealed record InvoiceParticipantWrite(
    string? EconomicCode, string? BranchCode, string? PostalCode,
    string? Province, string? City, string? Address, string? Phone,
    string? ForeignNationalCode, string? PassportNumber, string? ContractReference,
    long ExpectedRevision);
public sealed record InvoiceParticipantResult(InvoiceParticipantOutcome Outcome,
    InvoiceParticipantDetails? Details = null);

/// <summary>Scoped fiscal header metadata. NOT fiscal verification or a credential store.</summary>
public sealed class InvoiceParticipantDetailsService(
    ArginTaxPayerDbContext db, IdentityAuthorizationService identity)
{
    private Task<bool> AdminScopeAsync(Guid actor, Guid customer, bool seller, CancellationToken ct) =>
        db.IdentityUsers.AsNoTracking().AnyAsync(u => u.Id == actor && u.IsActive &&
            db.IdentityMfaStates.Any(m => m.UserId == u.Id && m.EnabledAtUtc != null) &&
            db.CustomerCommercialOwners.Any(o => o.CustomerUserId == customer &&
                (u.Roles.Any(r => r.Role == IdentityRoleNames.Level1) && o.RootLevel1UserId == actor ||
                !seller && u.Roles.Any(r => r.Role == IdentityRoleNames.Level2) &&
                o.PartnerLevel2UserId == actor)), ct);

    private Task<bool> ValidOwnerAsync(Guid customer, Guid subject, bool seller, CancellationToken ct) =>
        seller
            ? db.TaxpayerProfiles.AsNoTracking().AnyAsync(t =>
                t.Id == subject && t.CustomerUserId == customer, ct)
            : db.CustomerPartyLinks.AsNoTracking().AnyAsync(link =>
                link.PartyId == subject && link.CustomerUserId == customer &&
                link.UnlinkedAtUtc == null &&
                db.Parties.Any(p => p.Id == subject && p.Status == PartyStatus.Active), ct);

    private static string? Normalize(string? s, int max, bool digitsOnly = false)
    {
        if (s is null) return null;
        if (s.Length > max * 3 || s.Any(char.IsControl)) throw new ArgumentException();
        var v = s.Trim().Normalize(NormalizationForm.FormC);
        if (v.Length == 0) return null;
        if (digitsOnly)
        {
            var chars = v.Select(ch => ch is >= '۰' and <= '۹' ? (char)('0' + ch - '۰') :
                ch is >= '٠' and <= '٩' ? (char)('0' + ch - '٠') : ch).ToArray();
            v = new string(chars);
            if (v.Any(ch => ch is < '0' or > '9')) throw new ArgumentException();
        }
        if (v.Length > max) throw new ArgumentException();
        return v;
    }

    private static InvoiceParticipantWrite? Clean(InvoiceParticipantWrite? input)
    {
        if (input is null || input.ExpectedRevision < 0) return null;
        try
        {
            var economic = Normalize(input.EconomicCode, 32, true);
            var branch = Normalize(input.BranchCode, 16, true);
            var postal = Normalize(input.PostalCode, 10, true);
            if (postal is not null && postal.Length != 10) return null;
            var province = Normalize(input.Province, 120);
            var city = Normalize(input.City, 120);
            var address = Normalize(input.Address, 500);
            var phone = Normalize(input.Phone, 20);
            if (phone is not null && phone.Any(ch => ch is not (>= '0' and <= '9') and not '+' and not '-' and not ' ')) return null;
            var foreign = Normalize(input.ForeignNationalCode, 32);
            var passport = Normalize(input.PassportNumber, 32);
            var contract = Normalize(input.ContractReference, 64);
            return new(economic, branch, postal, province, city, address, phone, foreign,
                passport, contract, input.ExpectedRevision);
        }
        catch (ArgumentException) { return null; }
    }

    public async Task<InvoiceParticipantResult> GetAsync(
        string? bearer, Guid customer, Guid subject, bool seller, CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(bearer, ct);
        if (actor is null) return new(InvoiceParticipantOutcome.Unauthenticated);
        if (customer == Guid.Empty || subject == Guid.Empty) return new(InvoiceParticipantOutcome.Invalid);
        if (!await AdminScopeAsync(actor.Value, customer, seller, ct))
            return new(InvoiceParticipantOutcome.Forbidden);
        if (!await ValidOwnerAsync(customer, subject, seller, ct))
            return new(InvoiceParticipantOutcome.NotFound);
        var profile = await db.InvoiceParticipantDetails.AsNoTracking().SingleOrDefaultAsync(d =>
            d.CustomerUserId == customer &&
            (seller ? d.TaxpayerId == subject : d.PartyId == subject), ct);
        return new(InvoiceParticipantOutcome.Success, profile);
    }

    public async Task<InvoiceParticipantResult> SaveAsync(
        string? bearer, Guid customer, Guid subject, bool seller,
        InvoiceParticipantWrite? input, CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(bearer, ct);
        if (actor is null) return new(InvoiceParticipantOutcome.Unauthenticated);
        if (customer == Guid.Empty || subject == Guid.Empty) return new(InvoiceParticipantOutcome.Invalid);
        var clean = Clean(input);
        if (clean is null) return new(InvoiceParticipantOutcome.Invalid);
        if (!await AdminScopeAsync(actor.Value, customer, seller, ct))
            return new(InvoiceParticipantOutcome.Forbidden);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (!await AdminScopeAsync(actor.Value, customer, seller, ct))
                return new(InvoiceParticipantOutcome.Forbidden);
            if (!await ValidOwnerAsync(customer, subject, seller, ct))
                return new(InvoiceParticipantOutcome.NotFound);
            if (!seller && await db.CustomerPartyLinks.AsNoTracking().AnyAsync(l =>
                l.PartyId == subject && l.CustomerUserId != customer && l.UnlinkedAtUtc == null, ct))
                return new(InvoiceParticipantOutcome.Conflict);
            var row = await db.InvoiceParticipantDetails.SingleOrDefaultAsync(d =>
                d.CustomerUserId == customer &&
                (seller ? d.TaxpayerId == subject : d.PartyId == subject), ct);
            if (row is null)
            {
                if (clean.ExpectedRevision != 0) return new(InvoiceParticipantOutcome.Conflict);
                row = new InvoiceParticipantDetails
                {
                    Id = Guid.NewGuid(), CustomerUserId = customer,
                    PartyId = seller ? null : subject, TaxpayerId = seller ? subject : null,
                    CreatedAtUtc = DateTime.UtcNow
                };
                db.InvoiceParticipantDetails.Add(row);
            }
            else
            {
                if (row.Revision != clean.ExpectedRevision) return new(InvoiceParticipantOutcome.Conflict);
                row.Revision++;
            }
            row.EconomicCode = clean.EconomicCode;
            row.BranchCode = clean.BranchCode;
            row.PostalCode = clean.PostalCode;
            row.Province = clean.Province;
            row.City = clean.City;
            row.Address = clean.Address;
            row.Phone = clean.Phone;
            row.ForeignNationalCode = clean.ForeignNationalCode;
            row.PassportNumber = clean.PassportNumber;
            row.ContractReference = clean.ContractReference;
            row.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(InvoiceParticipantOutcome.Success, row);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return new(InvoiceParticipantOutcome.Conflict);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.CheckViolation })
        {
            db.ChangeTracker.Clear();
            return new(InvoiceParticipantOutcome.Conflict);
        }
    }
}
