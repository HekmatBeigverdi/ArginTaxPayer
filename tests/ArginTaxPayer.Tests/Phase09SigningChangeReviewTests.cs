using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Signing;
using ArginTaxPayer.Infrastructure.Signing.Review;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase09SigningChangeReviewTests
{
    private static readonly DateTime NowUtc =
        new(2026, 10, 21, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Approval_requires_a_reviewer_different_from_the_requester()
    {
        var request = CreateRequest();

        var result = CreateService().Decide(
            request,
            request.RequestedByUserId,
            approve: true,
            decisionReason: "approved",
            nowUtc: NowUtc);

        Assert.Equal(SigningChangeReviewStatus.Invalid, result.Status);
    }

    [Fact]
    public void Approved_request_is_one_time_and_expires_after_24_hours()
    {
        var result = CreateService().Decide(
            CreateRequest(), Guid.NewGuid(), approve: true, decisionReason: "approved", nowUtc: NowUtc);

        Assert.Equal(SigningChangeReviewStatus.Approved, result.Status);
        Assert.Equal(NowUtc.AddHours(24), result.Request.ExpiresAtUtc);
    }

    [Fact]
    public void Rejected_request_has_no_expiry()
    {
        var result = CreateService().Decide(
            CreateRequest(), Guid.NewGuid(), approve: false, decisionReason: "rejected", nowUtc: NowUtc);

        Assert.Equal(SigningChangeReviewStatus.Rejected, result.Status);
        Assert.Null(result.Request.ExpiresAtUtc);
    }

    [Fact]
    public void Pending_request_cannot_be_consumed()
    {
        var result = CreateService().Consume(
            CreateRequest(), Guid.NewGuid(), Guid.NewGuid(), NowUtc);

        Assert.Equal(SigningChangeReviewStatus.Invalid, result.Status);
    }

    [Fact]
    public void Approved_request_can_be_consumed()
    {
        var approved = Approve(CreateRequest());

        var result = CreateService().Consume(
            approved, Guid.NewGuid(), Guid.NewGuid(), NowUtc.AddHours(1));

        Assert.Equal(SigningChangeReviewStatus.Consumed, result.Status);
        Assert.Equal(SigningChangeReviewState.Consumed, result.Request.State);
    }

    [Fact]
    public void Approval_is_denied_at_the_expiry_boundary()
    {
        var approved = Approve(CreateRequest());

        var result = CreateService().Consume(
            approved, Guid.NewGuid(), Guid.NewGuid(), NowUtc.AddHours(24));

        Assert.Equal(SigningChangeReviewStatus.Expired, result.Status);
    }

    [Fact]
    public void Consumed_request_cannot_be_reused()
    {
        var consumed = Approve(CreateRequest()) with
        {
            State = SigningChangeReviewState.Consumed
        };

        var result = CreateService().Consume(
            consumed, Guid.NewGuid(), Guid.NewGuid(), NowUtc.AddHours(1));

        Assert.NotEqual(SigningChangeReviewStatus.Consumed, result.Status);
    }

    [Fact]
    public void Annual_reset_is_not_a_supported_review_kind()
    {
        Assert.False(Enum.IsDefined(typeof(SigningChangeKind), (short)99));
    }

    [Fact]
    public void Expected_revision_is_required()
    {
        var request = CreateRequest() with { ExpectedKeyRevision = 0 };

        var result = CreateService().Decide(
            request, Guid.NewGuid(), approve: true, decisionReason: "approved", nowUtc: NowUtc);

        Assert.Equal(SigningChangeReviewStatus.Invalid, result.Status);
    }

    [Fact]
    public void Control_characters_in_the_reason_are_rejected()
    {
        var request = CreateRequest() with { Reason = "bad\nreason" };

        var result = CreateService().Decide(
            request, Guid.NewGuid(), approve: true,
            decisionReason: "approved", nowUtc: NowUtc);

        Assert.Equal(SigningChangeReviewStatus.Invalid, result.Status);
    }

    private static SigningChangeReviewService CreateService() => new();

    private static SigningChangeReviewRequest Approve(SigningChangeReviewRequest request) =>
        CreateService().Decide(request, Guid.NewGuid(), approve: true,
            decisionReason: "approved", nowUtc: NowUtc).Request;

    private static SigningChangeReviewRequest CreateRequest() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        4,
        SigningChangeKind.Rotate,
        Guid.NewGuid(),
        "rotate signing key",
        NowUtc,
        SigningChangeReviewState.Pending);
}
