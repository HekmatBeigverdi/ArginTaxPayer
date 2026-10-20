using System;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase08Step15InternalReviewPolicyTests
{
    [Theory]
    [InlineData(null, "submit", true)]
    [InlineData("Draft", "submit", true)]
    [InlineData("ChangesRequested", "submit", true)]
    [InlineData("InReview", "submit", false)]
    [InlineData("Approved", "submit", false)]
    [InlineData("InReview", "approve", true)]
    [InlineData("InReview", "return", true)]
    [InlineData("Draft", "approve", false)]
    [InlineData("Approved", "return", false)]
    public void Review_topology_is_fail_closed(string? state, string action, bool valid)
        => Assert.Equal(valid, InvoiceInternalReviewPolicy.CanTransition(state,action));

    [Fact]
    public void Internal_reviewer_must_be_admin_and_different_from_submitter()
    {
        var id = Guid.NewGuid();
        Assert.False(InvoiceInternalReviewPolicy.ReviewerMayDecide(false,id,null));
        Assert.False(InvoiceInternalReviewPolicy.ReviewerMayDecide(true,id,id));
        Assert.True(InvoiceInternalReviewPolicy.ReviewerMayDecide(true,Guid.NewGuid(),id));
    }
}
