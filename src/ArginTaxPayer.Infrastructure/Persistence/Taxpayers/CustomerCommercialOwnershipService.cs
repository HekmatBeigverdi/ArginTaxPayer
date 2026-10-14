using ArginTaxPayer.Domain.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Taxpayers;

public enum CustomerOwnershipDecision { Success, Unauthenticated, Forbidden, Invalid, NotFound, Conflict, RootNotConfigured }
public sealed record TransferCustomerOwnerRequest(Guid? PartnerLevel2UserId, long ExpectedRevision,
    CustomerOwnershipChangeReason Reason, string? Explanation);
public sealed record CustomerCommercialOwnerView(Guid CustomerUserId, Guid RootLevel1UserId,
    Guid? PartnerLevel2UserId, CustomerAcquisitionChannel AcquisitionChannel, long Revision);
public sealed record CommercialPartnerOption(Guid UserId, string? Mobile, string? Email);
public sealed record CommercialPartnerListResponse(CustomerOwnershipDecision Decision,
    CommercialPartnerOption[]? Partners = null);
public sealed record CustomerAccountSummary(Guid CustomerUserId, Guid? RootLevel1UserId,
    Guid? PartnerLevel2UserId, CustomerAcquisitionChannel AcquisitionChannel,
    long Revision, string? Mobile, string? Email, DateTime CreatedAtUtc, bool IsActive,
    int TaxpayerCount);

public sealed record CustomerAccountListResponse(CustomerOwnershipDecision Decision,
    CustomerAccountSummary[]? Customers = null);
public sealed record CustomerTaxpayerListResponse(CustomerOwnershipDecision Decision,
    TaxpayerProfileView[]? Taxpayers = null);
public sealed record CustomerOwnershipResponse(CustomerOwnershipDecision Decision, CustomerCommercialOwnerView? Owner = null);

/// <summary>
/// Assigns one commercial account to an entire customer. Does NOT assign
/// individual fiscal taxpayers or retroactively rewrite booked transactions.
/// </summary>
public sealed class CustomerCommercialOwnershipService(
    ArginTaxPayerDbContext db, IdentityAuthorizationService identity, IConfiguration configuration)
{
    private Task<bool> IsScopedAdministratorAsync(Guid id, string role, CancellationToken ct) =>
        db.IdentityUsers.AsNoTracking().AnyAsync(u => u.Id == id && u.IsActive &&
            u.Roles.Any(r => r.Role == role) &&
            db.IdentityMfaStates.Any(m => m.UserId == id && m.EnabledAtUtc != null), ct);

    public async Task<Guid?> ResolveRootAsync(Guid? creatorLevel1Id, CancellationToken ct = default)
    {
        if (creatorLevel1Id is { } directRoot)
            return await IsRoleAsync(directRoot, IdentityRoleNames.Level1, ct) ? directRoot : null;
        var configured = configuration["Ownership:DefaultLevel1UserId"];
        if (!string.IsNullOrWhiteSpace(configured))
            return Guid.TryParse(configured, out var rootId) &&
                await IsRoleAsync(rootId, IdentityRoleNames.Level1, ct) ? rootId : null;
        // Ambiguous Level1 owners cannot silently acquire someone else's customers.
        var candidates = await db.IdentityUsers.AsNoTracking()
            .Where(u => u.IsActive && u.Roles.Any(r => r.Role == IdentityRoleNames.Level1))
            .Select(u => u.Id).Take(2).ToArrayAsync(ct);
        return candidates.Length == 1 ? candidates[0] : null;
    }

    private Task<bool> IsRoleAsync(Guid id, string role, CancellationToken ct) =>
        db.IdentityUsers.AsNoTracking().AnyAsync(u => u.Id == id && u.IsActive &&
            u.Roles.Any(r => r.Role == role), ct);

    public async Task<CustomerCommercialOwner?> PrepareInitialAsync(Guid customerUserId,
        Guid? creatingAdminUserId, CancellationToken ct = default)
    {
        Guid? level1 = null;
        Guid? level2 = null;
        if (creatingAdminUserId is { } creator)
        {
            if (await IsRoleAsync(creator, IdentityRoleNames.Level1, ct)) level1 = creator;
            else if (await IsRoleAsync(creator, IdentityRoleNames.Level2, ct)) level2 = creator;
            else return null;
        }
        var root = await ResolveRootAsync(level1, ct);
        if (root is null) return null;
        var channel = creatingAdminUserId is null ? CustomerAcquisitionChannel.PublicSelfRegistration :
            level1 is not null ? CustomerAcquisitionChannel.Level1Created :
            CustomerAcquisitionChannel.Level2Created;
        var now = DateTime.UtcNow;
        return new CustomerCommercialOwner
        {
            CustomerUserId = customerUserId, RootLevel1UserId = root.Value,
            PartnerLevel2UserId = CustomerCommercialOwnershipPolicy.InitialPartner(channel, level2),
            AcquisitionChannel = channel, Revision = 1,
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
    }

    public async Task<CustomerOwnershipResponse> GetAsync(string? bearer, Guid customerUserId,
        CancellationToken ct = default)
    {
        var actorId = await identity.CurrentUserIdAsync(bearer, ct);
        if (actorId is null) return new(CustomerOwnershipDecision.Unauthenticated);
        var isRoot = await IsScopedAdministratorAsync(actorId.Value, IdentityRoleNames.Level1, ct);
        var isPartner = await IsScopedAdministratorAsync(actorId.Value, IdentityRoleNames.Level2, ct);
        if (!isRoot && !isPartner) return new(CustomerOwnershipDecision.Forbidden);
        var record = await db.CustomerCommercialOwners.AsNoTracking().Where(o =>
            o.CustomerUserId == customerUserId &&
            (isRoot && o.RootLevel1UserId == actorId.Value ||
             isPartner && o.PartnerLevel2UserId == actorId.Value))
            .Select(o => new CustomerCommercialOwnerView(o.CustomerUserId,
                o.RootLevel1UserId, o.PartnerLevel2UserId,
                o.AcquisitionChannel, o.Revision)).SingleOrDefaultAsync(ct);
        return record is null ? new(CustomerOwnershipDecision.NotFound) :
            new(CustomerOwnershipDecision.Success, record);
    }

    public async Task<(CustomerOwnershipDecision Decision, CustomerCommercialOwnerView[]? Owners)> ListAsync(
        string? bearer, CancellationToken ct = default)
    {
        var actorId = await identity.CurrentUserIdAsync(bearer, ct);
        if (actorId is null) return (CustomerOwnershipDecision.Unauthenticated, null);
        var root = await IsScopedAdministratorAsync(actorId.Value, IdentityRoleNames.Level1, ct);
        var partner = await IsScopedAdministratorAsync(actorId.Value, IdentityRoleNames.Level2, ct);
        if (!root && !partner) return (CustomerOwnershipDecision.Forbidden, null);
        var result = await db.CustomerCommercialOwners.AsNoTracking()
            .Where(o => root && o.RootLevel1UserId == actorId.Value ||
                partner && o.PartnerLevel2UserId == actorId.Value)
            .OrderBy(o => o.CreatedAtUtc)
            .Select(o => new CustomerCommercialOwnerView(o.CustomerUserId, o.RootLevel1UserId,
                o.PartnerLevel2UserId, o.AcquisitionChannel, o.Revision)).ToArrayAsync(ct);
        return (CustomerOwnershipDecision.Success, result);
    }

    public async Task<(CustomerOwnershipDecision Decision, CustomerCommercialOwnerChange[]? History)>
        HistoryAsync(string? bearer, Guid customerUserId, CancellationToken ct = default)
    {
        var actorId = await identity.CurrentUserIdAsync(bearer, ct);
        if (actorId is null) return (CustomerOwnershipDecision.Unauthenticated, null);
        if (!await db.IdentityUsers.AsNoTracking().AnyAsync(u =>
            u.Id == actorId && u.IsActive &&
            u.Roles.Any(r => r.Role == IdentityRoleNames.Level1) &&
            db.IdentityMfaStates.Any(m => m.UserId == u.Id && m.EnabledAtUtc != null), ct))
            return (CustomerOwnershipDecision.Forbidden, null);
        if (!await db.CustomerCommercialOwners.AsNoTracking().AnyAsync(o =>
            o.CustomerUserId == customerUserId && o.RootLevel1UserId == actorId, ct))
            return (CustomerOwnershipDecision.NotFound, null);
        var history = await db.CustomerCommercialOwnerChanges.AsNoTracking()
            .Where(h => h.CustomerUserId == customerUserId)
            .OrderBy(h => h.ChangedAtUtc).ThenBy(h => h.ToRevision)
            .ToArrayAsync(ct);
        return (CustomerOwnershipDecision.Success, history);
    }

    /// <summary>Level1-only directory of eligible active Level2 partner identities.</summary>
    public async Task<CommercialPartnerListResponse> ListPartnersAsync(
        string? bearer, CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(bearer, ct);
        if (actor is null) return new(CustomerOwnershipDecision.Unauthenticated);
        if (!await IsScopedAdministratorAsync(actor.Value, IdentityRoleNames.Level1, ct))
            return new(CustomerOwnershipDecision.Forbidden);
        var partners = await db.IdentityUsers.AsNoTracking()
            .Where(u => u.IsActive && u.Roles.Any(r => r.Role == IdentityRoleNames.Level2) &&
                !u.Roles.Any(r => r.Role == IdentityRoleNames.Level1) &&
                db.IdentityMfaStates.Any(m => m.UserId == u.Id && m.EnabledAtUtc != null))
            .OrderBy(u => u.NormalizedMobile).ThenBy(u => u.Id)
            .Select(u => new CommercialPartnerOption(u.Id, u.NormalizedMobile, u.NormalizedEmail))
            .ToArrayAsync(ct);
        return new(CustomerOwnershipDecision.Success, partners);
    }

    /// <summary>Customer-first administrator listing; scoped before any materialization.</summary>
    public async Task<CustomerAccountListResponse> ListCustomerAccountsAsync(
        string? bearer, CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(bearer, ct);
        if (actor is null) return new(CustomerOwnershipDecision.Unauthenticated);
        var root = await IsScopedAdministratorAsync(actor.Value, IdentityRoleNames.Level1, ct);
        var partner = await IsScopedAdministratorAsync(actor.Value, IdentityRoleNames.Level2, ct);
        if (!root && !partner) return new(CustomerOwnershipDecision.Forbidden);
        // Root sees all registered customer identities, including legacy accounts
        // awaiting supervised attribution. Partners only see explicitly owned accounts.
        var rows = await (
            from u in db.IdentityUsers.AsNoTracking()
            where u.Roles.Any(role => role.Role == IdentityRoleNames.Customer)
            join assignment in db.CustomerCommercialOwners.AsNoTracking()
                on u.Id equals assignment.CustomerUserId into matches
            from o in matches.DefaultIfEmpty()
            where root || partner && o != null && o.PartnerLevel2UserId == actor.Value
            orderby u.CreatedAtUtc descending, u.Id
            select new CustomerAccountSummary(
                u.Id, o == null ? null : o.RootLevel1UserId,
                o == null ? null : o.PartnerLevel2UserId,
                o == null ? 0 : o.AcquisitionChannel,
                o == null ? 0 : o.Revision,
                u.NormalizedMobile, u.NormalizedEmail, u.CreatedAtUtc, u.IsActive,
                db.TaxpayerProfiles.Count(t => t.CustomerUserId == u.Id))
        ).ToArrayAsync(ct);
        return new(CustomerOwnershipDecision.Success, rows);
    }

    /// <summary>Taxpayers belong to the selected customer; never fetch unrelated profiles.</summary>
    public async Task<CustomerTaxpayerListResponse> ListCustomerTaxpayersAsync(
        string? bearer, Guid customerId, CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(bearer, ct);
        if (actor is null) return new(CustomerOwnershipDecision.Unauthenticated);
        var root = await IsScopedAdministratorAsync(actor.Value, IdentityRoleNames.Level1, ct);
        var partner = await IsScopedAdministratorAsync(actor.Value, IdentityRoleNames.Level2, ct);
        if (!root && !partner) return new(CustomerOwnershipDecision.Forbidden);
        var permitted = db.IdentityUsers.AsNoTracking().Where(u => u.Id == customerId &&
            u.Roles.Any(role => role.Role == IdentityRoleNames.Customer) &&
            (root || partner && db.CustomerCommercialOwners.Any(o =>
                o.CustomerUserId == u.Id && o.PartnerLevel2UserId == actor.Value)));
        var profiles = await db.TaxpayerProfiles.AsNoTracking().Where(t =>
            t.CustomerUserId == customerId && permitted.Any())
            .OrderBy(t => t.DisplayName).ThenBy(t => t.Id)
            .Select(t => new TaxpayerProfileView(t.Id, t.Kind, t.NormalizedIdentifier,
                t.DisplayName, t.Status, t.Revision))
            .ToArrayAsync(ct);
        if (!await permitted.AnyAsync(ct)) return new(CustomerOwnershipDecision.NotFound);
        return new(CustomerOwnershipDecision.Success, profiles);
    }

    public async Task<CustomerOwnershipResponse> TransferAsync(string? bearer, Guid customerUserId,
        TransferCustomerOwnerRequest request, CancellationToken ct = default)
    {
        var actorId = await identity.CurrentUserIdAsync(bearer, ct);
        if (actorId is null) return new(CustomerOwnershipDecision.Unauthenticated);
        var isLevel1 = await db.IdentityUsers.AsNoTracking().AnyAsync(u =>
            u.Id == actorId && u.IsActive &&
            u.Roles.Any(r => r.Role == IdentityRoleNames.Level1) &&
            db.IdentityMfaStates.Any(m => m.UserId == actorId && m.EnabledAtUtc != null), ct);
        if (!isLevel1) return new(CustomerOwnershipDecision.Forbidden);
        if (customerUserId == Guid.Empty || request is null || request.ExpectedRevision < 0 ||
            !Enum.IsDefined(request.Reason) || string.IsNullOrWhiteSpace(request.Explanation))
            return new(CustomerOwnershipDecision.Invalid);
        var explanation = request.Explanation.Trim().Normalize(System.Text.NormalizationForm.FormC);
        if (explanation.Length > 1000 || explanation.Any(char.IsControl))
            return new(CustomerOwnershipDecision.Invalid);
        if (request.PartnerLevel2UserId is { } partner &&
            !await db.IdentityUsers.AsNoTracking().AnyAsync(u =>
                u.Id == partner && u.IsActive && u.Roles.Any(r => r.Role == IdentityRoleNames.Level2) &&
                !u.Roles.Any(r => r.Role == IdentityRoleNames.Level1) &&
                db.IdentityMfaStates.Any(m => m.UserId == partner && m.EnabledAtUtc != null), ct))
            return new(CustomerOwnershipDecision.Forbidden);
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // Serializable owner change conflicts with concurrent partner-scoped edits.
            await using var tx = await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, ct);
            if (await identity.CurrentUserIdAsync(bearer, ct) != actorId ||
                !await IsScopedAdministratorAsync(actorId.Value, IdentityRoleNames.Level1, ct))
                return new CustomerOwnershipResponse(CustomerOwnershipDecision.Forbidden);
            if (request.PartnerLevel2UserId is { } proposedPartner &&
                !await db.IdentityUsers.AsNoTracking().AnyAsync(u =>
                    u.Id == proposedPartner && u.IsActive &&
                    u.Roles.Any(r => r.Role == IdentityRoleNames.Level2) &&
                    !u.Roles.Any(r => r.Role == IdentityRoleNames.Level1) &&
                    db.IdentityMfaStates.Any(m => m.UserId == proposedPartner && m.EnabledAtUtc != null), ct))
                return new CustomerOwnershipResponse(CustomerOwnershipDecision.Forbidden);
            var owner = await db.CustomerCommercialOwners.AsNoTracking()
                .SingleOrDefaultAsync(o => o.CustomerUserId == customerUserId && o.RootLevel1UserId == actorId, ct);
            if (owner is null)
            {
                // Legacy customer has no commercial-owner record. Level1 must
                // explicitly review this identity; never guess its acquisition source.
                if (request.ExpectedRevision != 0)
                    return new CustomerOwnershipResponse(CustomerOwnershipDecision.Conflict);
                var isCustomer = await db.IdentityUsers.AsNoTracking().AnyAsync(u =>
                    u.Id == customerUserId && u.Roles.Any(r => r.Role == IdentityRoleNames.Customer), ct);
                if (!isCustomer) return new CustomerOwnershipResponse(CustomerOwnershipDecision.NotFound);
                var createdAt = DateTime.UtcNow;
                var initial = new CustomerCommercialOwner
                {
                    CustomerUserId = customerUserId, RootLevel1UserId = actorId.Value,
                    PartnerLevel2UserId = request.PartnerLevel2UserId,
                    AcquisitionChannel = CustomerAcquisitionChannel.LegacyReconciledByLevel1,
                    Revision = 1, CreatedAtUtc = createdAt, UpdatedAtUtc = createdAt
                };
                db.CustomerCommercialOwners.Add(initial);
                db.CustomerCommercialOwnerChanges.Add(new CustomerCommercialOwnerChange
                {
                    Id = Guid.NewGuid(), CustomerUserId = customerUserId,
                    RootLevel1UserId = actorId.Value, PreviousPartnerLevel2UserId = null,
                    NewPartnerLevel2UserId = request.PartnerLevel2UserId,
                    ApprovedByLevel1UserId = actorId.Value, Reason = request.Reason,
                    Explanation = explanation, FromRevision = 0, ToRevision = 1,
                    ChangedAtUtc = createdAt
                });
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new CustomerOwnershipResponse(CustomerOwnershipDecision.Success,
                    new CustomerCommercialOwnerView(customerUserId, actorId.Value,
                        request.PartnerLevel2UserId, initial.AcquisitionChannel, 1));
            }
            if (owner.Revision != request.ExpectedRevision)
                return new CustomerOwnershipResponse(CustomerOwnershipDecision.Conflict);
            if (owner.PartnerLevel2UserId == request.PartnerLevel2UserId)
                return new CustomerOwnershipResponse(CustomerOwnershipDecision.Invalid);
            // Source state must be rechecked in SQL, not a previously cached UI decision.
            var updated = await db.CustomerCommercialOwners.Where(o => o.CustomerUserId == customerUserId &&
                o.RootLevel1UserId == actorId && o.Revision == request.ExpectedRevision &&
                o.PartnerLevel2UserId == owner.PartnerLevel2UserId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.PartnerLevel2UserId, request.PartnerLevel2UserId)
                    .SetProperty(x => x.Revision, x => x.Revision + 1)
                    .SetProperty(x => x.UpdatedAtUtc, DateTime.UtcNow), ct);
            if (updated != 1) return new CustomerOwnershipResponse(CustomerOwnershipDecision.Conflict);
            db.CustomerCommercialOwnerChanges.Add(new CustomerCommercialOwnerChange
            {
                Id = Guid.NewGuid(), CustomerUserId = customerUserId, RootLevel1UserId = owner.RootLevel1UserId,
                PreviousPartnerLevel2UserId = owner.PartnerLevel2UserId,
                NewPartnerLevel2UserId = request.PartnerLevel2UserId,
                ApprovedByLevel1UserId = actorId.Value, Reason = request.Reason,
                Explanation = explanation, FromRevision = owner.Revision, ToRevision = owner.Revision + 1,
                ChangedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new CustomerOwnershipResponse(CustomerOwnershipDecision.Success,
                new CustomerCommercialOwnerView(customerUserId, owner.RootLevel1UserId,
                    request.PartnerLevel2UserId, owner.AcquisitionChannel, owner.Revision + 1));
            });
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.SerializationFailure })
        {
            db.ChangeTracker.Clear();
            return new(CustomerOwnershipDecision.Conflict);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            db.ChangeTracker.Clear();
            return new(CustomerOwnershipDecision.Conflict);
        }
    }
}
