using ArginTaxPayer.Domain.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Parties;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Taxpayers;

public sealed record CreateTaxpayerRequest(TaxpayerKind Kind, string? Identifier, string? DisplayName, Guid? CustomerUserId = null);
public sealed record TaxpayerCreated(Guid Id, TaxpayerKind Kind, string NormalizedIdentifier, string DisplayName);
public enum CreateTaxpayerStatus { Created, Invalid, Unauthenticated, Forbidden, Conflict }
public sealed record CreateTaxpayerResult(CreateTaxpayerStatus Status, TaxpayerCreated? Taxpayer = null);

/// <summary>Scoped participant creation for active Customer/Level2/Level1. Does not grant invoice-submission entitlements.</summary>
public sealed class TaxpayerCreationService(
    ArginTaxPayerDbContext db,
    IdentityAuthorizationService authorization,
    ParticipantScopeService scope)
{
    public async Task<CreateTaxpayerResult> CreateAsync(
        string? accessToken, CreateTaxpayerRequest? request, CancellationToken ct = default)
    {
        var actor = await authorization.CurrentUserIdAsync(accessToken, ct);
        if (actor is null) return new(CreateTaxpayerStatus.Unauthenticated);
        if (request is null || request.CustomerUserId is null || request.CustomerUserId == Guid.Empty ||
            !Enum.IsDefined(request.Kind) ||
            !IranianTaxpayerIdentifier.TryNormalize(request.Kind, request.Identifier, out var identifier) ||
            string.IsNullOrWhiteSpace(request.DisplayName))
            return new(CreateTaxpayerStatus.Invalid);
        var name = request.DisplayName.Trim().Normalize(System.Text.NormalizationForm.FormC);
        if (name.Length > 256 || name.Any(char.IsControl))
            return new(CreateTaxpayerStatus.Invalid);

        if (!await scope.CanManageAsync(actor.Value, request.CustomerUserId.Value, ct))
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
