using ArginTaxPayer.IdentityOperator;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class IdentityOperatorCommandPolicyTests
{
    [Fact]
    public void ResetEnrollCommandRequiresAndNormalizesAnIranianMobile()
    {
        var parsed = OperatorCommandPolicy.Parse(["reset-enroll-level1-test", "0919 242 4119"]);

        Assert.Equal(OperatorCommand.ResetEnrollLevel1Test, parsed.Kind);
        Assert.Equal("+989192424119", parsed.Mobile);
    }

    [Fact]
    public void ResetEnrollCommandRejectsMissingOrInvalidMobile()
    {
        Assert.Null(OperatorCommandPolicy.TryParse(["reset-enroll-level1-test"]));
        Assert.Null(OperatorCommandPolicy.TryParse(["reset-enroll-level1-test", "123"]));
    }
}
