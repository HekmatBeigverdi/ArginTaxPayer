namespace ArginTaxPayer.Domain.Signing;

public enum SigningChangeReviewState : short { Pending = 1, Approved = 2, Rejected = 3, Consumed = 4, Expired = 5 }
public enum SigningChangeKind : short { Suspend = 1, Compromise = 2, Rotate = 3 }

public static class SigningChangeReviewPolicy
{
    public static bool CanTransition(SigningChangeReviewState from, SigningChangeReviewState to) =>
        (from, to) switch
        {
            (SigningChangeReviewState.Pending, SigningChangeReviewState.Approved or SigningChangeReviewState.Rejected) => true,
            (SigningChangeReviewState.Approved, SigningChangeReviewState.Consumed or SigningChangeReviewState.Expired) => true,
            _ => false
        };

    public static bool IsUsable(SigningChangeReviewState state, DateTime nowUtc, DateTime? expiresAtUtc) =>
        state == SigningChangeReviewState.Approved && expiresAtUtc is not null && expiresAtUtc > nowUtc;

    public static DateTime Expiry(DateTime reviewedAtUtc) => reviewedAtUtc.ToUniversalTime().AddHours(24);
}
