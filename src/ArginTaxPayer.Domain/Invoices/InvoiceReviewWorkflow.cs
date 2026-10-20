namespace ArginTaxPayer.Domain.Invoices;

/// <summary>
/// Phase08 internal review workflow. Independent from Phase07's
/// Draft/Validated/Finalized lifecycle and from TaxAuthorityProcessingState.
/// This pure proposal is NOT authorization, persistence, or a fiscal submission.
/// </summary>
public enum InvoiceReviewState
{
    Draft = 1,
    InReview = 2,
    ChangesRequested = 3,
    Approved = 4
}

public enum InvoiceReviewAction
{
    SubmitForReview = 1,
    RequestChanges = 2,
    Approve = 3
}

public static class InvoiceReviewPolicy
{
    public static bool CanApply(InvoiceReviewState state, InvoiceReviewAction action) =>
        (state is InvoiceReviewState.Draft or InvoiceReviewState.ChangesRequested &&
            action == InvoiceReviewAction.SubmitForReview) ||
        (state == InvoiceReviewState.InReview &&
            action is InvoiceReviewAction.RequestChanges or InvoiceReviewAction.Approve);
}

/// <summary>
/// All authorization, trusted invoice-validation and invoice-revision checks
/// must be performed by the application service before persisting this proposal.
/// Review revisions are distinct from invoice aggregate revisions.
/// </summary>
public sealed record InvoiceReviewSnapshot
{
    public Guid InvoiceId { get; }
    public InvoiceReviewState State { get; }
    public long Revision { get; }
    public DateTime UpdatedAtUtc { get; }
    public Guid? LastActorUserId { get; }
    public string? ChangeReason { get; }

    private InvoiceReviewSnapshot(Guid invoiceId, InvoiceReviewState state, long revision,
        DateTime updatedAtUtc, Guid? lastActorUserId, string? changeReason)
    {
        InvoiceId = invoiceId;
        State = state;
        Revision = revision;
        UpdatedAtUtc = updatedAtUtc;
        LastActorUserId = lastActorUserId;
        ChangeReason = changeReason;
    }

    public static InvoiceReviewSnapshot NewDraft(Guid invoiceId, DateTime atUtc)
    {
        if (invoiceId == Guid.Empty || atUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Review draft requires a valid invoice and UTC timestamp.");
        return new(invoiceId, InvoiceReviewState.Draft, 1, atUtc, null, null);
    }

    /// <summary>
    /// Constructs a new immutable proposed state; never changes the source snapshot.
    /// A storage adapter must compare-and-swap Revision transactionally.
    /// </summary>
    public InvoiceReviewSnapshot Propose(InvoiceReviewAction action, Guid actorUserId,
        long expectedRevision, DateTime atUtc, string? reason = null)
    {
        if (!Enum.IsDefined(action) || actorUserId == Guid.Empty ||
            atUtc.Kind != DateTimeKind.Utc || atUtc < UpdatedAtUtc)
            throw new ArgumentException("Invalid review action, actor or UTC timestamp.");
        if (expectedRevision != Revision)
            throw new InvalidOperationException("Stale review revision.");
        if (!InvoiceReviewPolicy.CanApply(State, action))
            throw new InvalidOperationException("Review state transition not allowed.");

        if (action == InvoiceReviewAction.RequestChanges)
        {
            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000 ||
                reason.Any(char.IsControl))
                throw new ArgumentException("A safe change-request reason is required.", nameof(reason));
        }
        else if (reason is not null)
            throw new ArgumentException("Only a change request may contain a reason.", nameof(reason));

        var next = action switch
        {
            InvoiceReviewAction.SubmitForReview => InvoiceReviewState.InReview,
            InvoiceReviewAction.RequestChanges => InvoiceReviewState.ChangesRequested,
            InvoiceReviewAction.Approve => InvoiceReviewState.Approved,
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
        return new(InvoiceId, next, checked(Revision + 1), atUtc, actorUserId,
            action == InvoiceReviewAction.RequestChanges ? reason!.Trim() : null);
    }
}
