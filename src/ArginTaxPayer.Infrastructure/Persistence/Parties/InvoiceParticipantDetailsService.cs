using System.Data;
using System.Text.Json;
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
    ArginTaxPayerDbContext db, IdentityAuthorizationService identity, ParticipantScopeService scope)
{
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
            if (phone is not null)
                phone = new string(phone.Select(ch => ch is >= '۰' and <= '۹'
                    ? (char)('0' + ch - '۰') : ch is >= '٠' and <= '٩'
                    ? (char)('0' + ch - '٠') : ch).ToArray());
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
        if (!await scope.CanManageAsync(actor.Value, customer, ct))
            return new(InvoiceParticipantOutcome.Forbidden);
        if (!await ValidOwnerAsync(customer, subject, seller, ct))
            return new(InvoiceParticipantOutcome.NotFound);
        var profile = await db.InvoiceParticipantDetails.AsNoTracking().SingleOrDefaultAsync(d =>
            d.CustomerUserId == customer &&
            (seller ? d.TaxpayerId == subject : d.PartyId == subject), ct);
        return new(InvoiceParticipantOutcome.Success, profile);
    }

    /// <summary>
    /// Partial, revision-guarded updates: omitted fields retain their prior persisted values.
    /// Explicit JSON null/empty string clears only that field. All fields are optional.
    /// </summary>
    public async Task<InvoiceParticipantResult> PatchAsync(string? bearer, Guid customer,
        Guid subject, bool seller, JsonElement changes, CancellationToken ct = default)
    {
        if (changes.ValueKind != JsonValueKind.Object ||
            !changes.TryGetProperty("expectedRevision", out var expected) ||
            expected.ValueKind != JsonValueKind.Number || !expected.TryGetInt64(out var revision) ||
            revision < 0) return new(InvoiceParticipantOutcome.Invalid);
        var current = await GetAsync(bearer, customer, subject, seller, ct);
        if (current.Outcome != InvoiceParticipantOutcome.Success) return current;
        if ((current.Details?.Revision ?? 0) != revision)
            return new(InvoiceParticipantOutcome.Conflict);

        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["economicCode"] = current.Details?.EconomicCode,
            ["branchCode"] = current.Details?.BranchCode,
            ["postalCode"] = current.Details?.PostalCode,
            ["province"] = current.Details?.Province,
            ["city"] = current.Details?.City,
            ["address"] = current.Details?.Address,
            ["phone"] = current.Details?.Phone,
            ["foreignNationalCode"] = current.Details?.ForeignNationalCode,
            ["passportNumber"] = current.Details?.PassportNumber,
            ["contractReference"] = current.Details?.ContractReference
        };
        var touched = false;
        foreach (var property in changes.EnumerateObject())
        {
            if (property.Name == "expectedRevision") continue;
            if (!values.ContainsKey(property.Name) ||
                property.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
                return new(InvoiceParticipantOutcome.Invalid);
            values[property.Name] = property.Value.ValueKind == JsonValueKind.Null
                ? null : property.Value.GetString();
            touched = true;
        }
        if (!touched) return new(InvoiceParticipantOutcome.Invalid);
        return await SaveAsync(bearer, customer, subject, seller, new InvoiceParticipantWrite(
            values["economicCode"], values["branchCode"], values["postalCode"],
            values["province"], values["city"], values["address"], values["phone"],
            values["foreignNationalCode"], values["passportNumber"],
            values["contractReference"], revision), ct);
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
        if (!await scope.CanManageAsync(actor.Value, customer, ct))
            return new(InvoiceParticipantOutcome.Forbidden);
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync<InvoiceParticipantResult>(async () =>
            {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (!await scope.CanManageAsync(actor.Value, customer, ct))
                return new(InvoiceParticipantOutcome.Forbidden);
            if (!await ValidOwnerAsync(customer, subject, seller, ct))
                return new(InvoiceParticipantOutcome.NotFound);
            // The fiscal metadata row is keyed by (customer, party/taxpayer).
            // A Party shared between customers does not prevent editing THIS account's
            // own metadata. We do not update the underlying shared Party identity here.
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
            });
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
