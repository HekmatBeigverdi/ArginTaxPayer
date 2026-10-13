using ArginTaxPayer.Domain.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Taxpayers;

public sealed record CreateTaxpayerRequest(TaxpayerKind Kind, string? Identifier, string? DisplayName, Guid? CustomerUserId = null);
public sealed record TaxpayerCreated(Guid Id, TaxpayerKind Kind, string NormalizedIdentifier, string DisplayName);
public enum CreateTaxpayerStatus { Created, Invalid, Unauthenticated, Forbidden, Conflict }
public sealed record CreateTaxpayerResult(CreateTaxpayerStatus Status, TaxpayerCreated? Taxpayer = null);

/// <summary>Level 1 + enrolled MFA only. Never trusts role claims, actor IDs or tenant IDs in request bodies.</summary>
public sealed class TaxpayerCreationService(
    ArginTaxPayerDbContext db,
    IdentityAuthorizationService authorization)
{
    public async Task<CreateTaxpayerResult> CreateAsync(
        string? accessToken, CreateTaxpayerRequest? request, CancellationToken ct = default)
    {
        var actor = await authorization.CurrentUserIdAsync(accessToken, ct);
        if (actor is null) return new(CreateTaxpayerStatus.Unauthenticated);
        // Explicitly check active Level 1 plus MFA. General administration.read allows Level 2.
        if (!await db.IdentityUsers.AsNoTracking().AnyAsync(u =>
            u.Id == actor.Value && u.IsActive &&
            u.Roles.Any(r => r.Role == IdentityRoleNames.Level1) &&
            db.IdentityMfaStates.Any(m => m.UserId == u.Id && m.EnabledAtUtc != null), ct))
            return new(CreateTaxpayerStatus.Forbidden);

        if (request is null || request.CustomerUserId is null || request.CustomerUserId == Guid.Empty ||
            !Enum.IsDefined(request.Kind) ||
            !IranianTaxpayerIdentifier.TryNormalize(request.Kind, request.Identifier, out var identifier) ||
            string.IsNullOrWhiteSpace(request.DisplayName))
            return new(CreateTaxpayerStatus.Invalid);
        var name = request.DisplayName.Trim().Normalize(System.Text.NormalizationForm.FormC);
        if (name.Length > 256 || name.Any(char.IsControl))
            return new(CreateTaxpayerStatus.Invalid);

        // Identity account membership is independent from partner ownership.
        // Never silently infer a Level2 partner from a fiscal-memory identifier.
        if (!await db.CustomerCommercialOwners.AsNoTracking().AnyAsync(o =>
            o.CustomerUserId == request.CustomerUserId && o.RootLevel1UserId == actor.Value, ct))
            return new(CreateTaxpayerStatus.Forbidden);

        var now = DateTime.UtcNow;
        var profile = new TaxpayerProfile
        {
            Id = Guid.NewGuid(), CustomerUserId = request.CustomerUserId,
            Kind = request.Kind, NormalizedIdentifier = identifier,
            DisplayName = name, Status = TaxpayerStatus.Active, Revision = 1,
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        try
        {
            db.TaxpayerProfiles.Add(profile);
            await db.SaveChangesAsync(ct);
            return new(CreateTaxpayerStatus.Created,
                new TaxpayerCreated(profile.Id, profile.Kind, identifier, name));
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.Entry(profile).State = EntityState.Detached;
            return new(CreateTaxpayerStatus.Conflict);
        }
    }
}
