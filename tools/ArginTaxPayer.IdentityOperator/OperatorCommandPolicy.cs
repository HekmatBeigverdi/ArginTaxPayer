using ArginTaxPayer.Infrastructure.Persistence.Identity;

namespace ArginTaxPayer.IdentityOperator;

public enum OperatorCommand
{
    ResetEnrollLevel1Test,
}

public sealed record OperatorCommandSpec(OperatorCommand Kind, string Mobile);

public static class OperatorCommandPolicy
{
    public static OperatorCommandSpec Parse(string[] args) =>
        TryParse(args) ?? throw new ArgumentException("A valid operator command and Iranian mobile are required.");

    public static OperatorCommandSpec? TryParse(string[] args)
    {
        if (args.Length != 2 || args[0] != "reset-enroll-level1-test" ||
            !IranianMobileNumber.TryNormalize(args[1], out var mobile))
            return null;

        return new(OperatorCommand.ResetEnrollLevel1Test, mobile);
    }
}
