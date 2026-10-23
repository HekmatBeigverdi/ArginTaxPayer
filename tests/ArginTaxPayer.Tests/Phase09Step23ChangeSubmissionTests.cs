using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Signing;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase09Step23ChangeSubmissionTests
{
    private static SigningChangeRequestInput Valid() =>
        new(Guid.NewGuid(), 2, SigningChangeKind.Rotate, "technical_issue",
            "The registered key must be replaced after an authorized request.");

    [Fact]
    public void Accepts_versioned_change_request_with_plain_nonsecret_reason() =>
        Assert.True(SigningChangeSubmissionPolicy.Valid(Valid()));

    [Fact]
    public void Rejects_missing_request_identifier() =>
        Assert.False(SigningChangeSubmissionPolicy.Valid(Valid() with { RequestId = Guid.Empty }));

    [Fact]
    public void Rejects_stale_or_zero_expected_revision() =>
        Assert.False(SigningChangeSubmissionPolicy.Valid(Valid() with { ExpectedKeyRevision = 0 }));

    [Fact]
    public void Rejects_outside_change_type() =>
        Assert.False(SigningChangeSubmissionPolicy.Valid(Valid() with { Kind = (SigningChangeKind)99 }));

    [Theory]
    [InlineData("Bad Uppercase")]
    [InlineData("a")]
    [InlineData("reason\nwith-newline")]
    public void Rejects_invalid_reason_code(string code) =>
        Assert.False(SigningChangeSubmissionPolicy.Valid(Valid() with { ReasonCode = code }));

    [Fact]
    public void Rejects_control_characters_and_overlong_explanation()
    {
        Assert.False(SigningChangeSubmissionPolicy.Valid(Valid() with { Explanation = "invalid\nreason text" }));
        Assert.False(SigningChangeSubmissionPolicy.Valid(Valid() with { Explanation = new string('a', 1001) }));
    }
}
