using System.Data;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public enum InvoiceReviewCommandDecision
{
    Success, Unauthenticated, Forbidden, NotFound, Invalid, Conflict
}

public sealed record InvoiceReviewCommand(
    Guid InvoiceId, long ExpectedReviewRevision, InvoiceReviewAction Action,
    string? ChangeReason = null);

public sealed record InvoiceReviewCommandResult(
    InvoiceReviewCommandDecision Decision, InvoiceReviewState? State = null,
    long? Revision = null);

/// <summary>
/// Internal workflow only. This service neither finalizes Phase07 documents nor
/// signs, transmits, or acknowledges tax-authority invoices.
/// </summary>
public sealed class InvoiceReviewCommandService(
    ArginTaxPayerDbContext db, TaxpayerScopeService scope)
{
    public async Task<InvoiceReviewCommandResult> ExecuteAsync(
        string? token, InvoiceReviewCommand? command, CancellationToken ct = default)
    {
        if (command is null || command.InvoiceId == Guid.Empty ||
            command.ExpectedReviewRevision < 1 || !Enum.IsDefined(command.Action))
            return new(InvoiceReviewCommandDecision.Invalid);
        if (command.Action == InvoiceReviewAction.RequestChanges &&
            (string.IsNullOrWhiteSpace(command.ChangeReason) ||
             command.ChangeReason.Length > 1000 ||
             command.ChangeReason.Any(char.IsControl)) ||
            command.Action != InvoiceReviewAction.RequestChanges &&
            command.ChangeReason is not null)
            return new(InvoiceReviewCommandDecision.Invalid);

        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable, ct);
                var principal = await scope.ResolveAsync(token, ct);
                if (principal.Decision == TaxpayerScopeDecision.Unauthenticated)
                    return new InvoiceReviewCommandResult(InvoiceReviewCommandDecision.Unauthenticated);
                if (principal.Decision != TaxpayerScopeDecision.Allowed)
                    return new InvoiceReviewCommandResult(InvoiceReviewCommandDecision.Forbidden);

                // Use a live, SQL-enforced scope before exposing invoice details.
                var invoice = await db.InvoiceDocuments.AsNoTracking()
                    .Where(i => i.Id == command.InvoiceId &&
                        scope.ScopedCommandQuery(principal.ActorId, principal.Level1)
                            .Any(t => t.Id == i.TaxpayerId && t.CustomerUserId == i.CustomerUserId))
                    .Select(i => new { i.Id, i.CustomerUserId, i.TaxpayerId, i.State })
                    .SingleOrDefaultAsync(ct);
                if (invoice is null)
                    return new InvoiceReviewCommandResult(InvoiceReviewCommandDecision.NotFound);

                // The active customer->taxpayer association is rechecked for each mutation.
                var membership = await db.CustomerTaxpayerMemberships.AsNoTracking()
                    .AnyAsync(x => x.CustomerUserId == invoice.CustomerUserId &&
                        x.TaxpayerId == invoice.TaxpayerId && x.UnlinkedAtUtc == null, ct);
                if (!membership)
                    return new InvoiceReviewCommandResult(InvoiceReviewCommandDecision.Forbidden);

                var actor = await db.IdentityUsers.AsNoTracking()
                    .Where(x => x.Id == principal.ActorId && x.IsActive)
                    .Select(x => new {
                        Customer = x.Roles.Any(y => y.Role == IdentityRoleNames.Customer),
                        Root = x.Roles.Any(y => y.Role == IdentityRoleNames.Level1),
                        Partner = x.Roles.Any(y => y.Role == IdentityRoleNames.Level2),
                        Mfa = db.IdentityMfaStates.Any(y => y.UserId == x.Id &&
                            y.EnabledAtUtc != null)
                    }).SingleOrDefaultAsync(ct);
                if (actor is null)
                    return new InvoiceReviewCommandResult(InvoiceReviewCommandDecision.Forbidden);

                bool isCustomer = actor.Customer && principal.ActorId == invoice.CustomerUserId;
                bool isRoot = actor.Root && actor.Mfa;
                bool isPartner = actor.Partner && actor.Mfa &&
                    await db.CustomerCommercialOwners.AsNoTracking()
                        .AnyAsync(x => x.CustomerUserId == invoice.CustomerUserId &&
                            x.PartnerLevel2UserId == principal.ActorId, ct);
                bool authorized = command.Action == InvoiceReviewAction.SubmitForReview
                    ? isCustomer : (isRoot || isPartner);
                if (!authorized)
                    return new InvoiceReviewCommandResult(InvoiceReviewCommandDecision.Forbidden);

                var now = DateTime.UtcNow;
                // No persisted review row means implicit Draft, revision 1.
                // The first successful transition inserts a row and event in one transaction.
                var current = await db.InvoiceReviews.AsNoTracking()
                    .Where(x => x.InvoiceId == invoice.Id &&
                        x.CustomerUserId == invoice.CustomerUserId &&
                        x.TaxpayerId == invoice.TaxpayerId)
                    .SingleOrDefaultAsync(ct);
                var state = current?.State ?? InvoiceReviewState.Draft;
                var revision = current?.Revision ?? 1L;
                if (command.ExpectedReviewRevision != revision)
                    return new InvoiceReviewCommandResult(InvoiceReviewCommandDecision.Conflict);
                if (!InvoiceReviewPolicy.CanApply(state, command.Action))
                    return new InvoiceReviewCommandResult(InvoiceReviewCommandDecision.Invalid);
                if (command.Action == InvoiceReviewAction.SubmitForReview &&
                    invoice.State != InvoiceLifecycleState.Validated)
                    return new InvoiceReviewCommandResult(InvoiceReviewCommandDecision.Invalid);

                InvoiceReviewSnapshot next;
                try
                {
                    // Reconstruct the snapshot through domain transitions.
                    // An existing cursor's preconditions are guaranteed by the database.
                    var candidate = InvoiceReviewSnapshot.NewDraft(invoice.Id, now);
                    if (current is null)
                        next = candidate.Propose(command.Action, principal.ActorId, revision, now,
                            command.ChangeReason);
                    else
                    {
                        var target = command.Action switch {
                            InvoiceReviewAction.SubmitForReview => InvoiceReviewState.InReview,
                            InvoiceReviewAction.RequestChanges => InvoiceReviewState.ChangesRequested,
                            InvoiceReviewAction.Approve => InvoiceReviewState.Approved,
                            _ => throw new InvalidOperationException()
                        };
                        next = candidate.Propose(InvoiceReviewAction.SubmitForReview,
                            principal.ActorId, 1, now);
                        // Existing cursor is handled using the guarded state policy above;
                        // persisted state and revision are set only via CAS.
                        _ = target;
                    }
                }
                catch (ArgumentException)
                {
                    return new InvoiceReviewCommandResult(InvoiceReviewCommandDecision.Invalid);
                }

                var toState = command.Action switch {
                    InvoiceReviewAction.SubmitForReview => InvoiceReviewState.InReview,
                    InvoiceReviewAction.RequestChanges => InvoiceReviewState.ChangesRequested,
                    InvoiceReviewAction.Approve => InvoiceReviewState.Approved,
                    _ => throw new InvalidOperationException()
                };
                var nextRevision = checked(revision + 1);
                if (current is null)
                {
                    db.InvoiceReviews.Add(new StoredInvoiceReview {
                        InvoiceId = invoice.Id, CustomerUserId = invoice.CustomerUserId,
                        TaxpayerId = invoice.TaxpayerId, State = toState,
                        Revision = nextRevision, UpdatedAtUtc = now,
                        LastActorUserId = principal.ActorId
                    });
                    await db.SaveChangesAsync(ct);
                }
                else
                {
                    var updated = await db.InvoiceReviews
                        .Where(x => x.InvoiceId == invoice.Id &&
                            x.CustomerUserId == invoice.CustomerUserId &&
                            x.TaxpayerId == invoice.TaxpayerId &&
                            x.Revision == revision && x.State == state)
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.State, toState)
                            .SetProperty(x => x.Revision, x => x.Revision + 1)
                            .SetProperty(x => x.UpdatedAtUtc, now)
                            .SetProperty(x => x.LastActorUserId, principal.ActorId), ct);
                    if (updated != 1)
                        return new InvoiceReviewCommandResult(InvoiceReviewCommandDecision.Conflict);
                }
                db.InvoiceReviewEvents.Add(new StoredInvoiceReviewEvent {
                    Id = Guid.NewGuid(), InvoiceId = invoice.Id,
                    CustomerUserId = invoice.CustomerUserId, TaxpayerId = invoice.TaxpayerId,
                    Revision = nextRevision, FromState = state, ToState = toState,
                    Action = command.Action, ActorUserId = principal.ActorId,
                    OccurredAtUtc = now,
                    ChangeReason = command.Action == InvoiceReviewAction.RequestChanges
                        ? command.ChangeReason!.Trim() : null
                });
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new InvoiceReviewCommandResult(InvoiceReviewCommandDecision.Success,
                    toState, nextRevision);
            });
        }
        catch (PostgresException ex) when (ex.SqlState is
            PostgresErrorCodes.SerializationFailure or
            PostgresErrorCodes.UniqueViolation)
        {
            db.ChangeTracker.Clear();
            return new(InvoiceReviewCommandDecision.Conflict);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
            pg.SqlState is PostgresErrorCodes.SerializationFailure or
                PostgresErrorCodes.UniqueViolation)
        {
            db.ChangeTracker.Clear();
            return new(InvoiceReviewCommandDecision.Conflict);
        }
    }
}
