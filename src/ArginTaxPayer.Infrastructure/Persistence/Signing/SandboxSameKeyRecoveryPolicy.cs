using System.Security.Cryptography;

namespace ArginTaxPayer.Infrastructure.Persistence.Signing;

public static class SandboxSameKeyRecoveryPolicy
{
    public static string? Validate(bool owner, bool freshLogin, bool consent, long expectedRevision,
        long currentRevision, ReadOnlySpan<byte> registeredPublicKey, ReadOnlySpan<byte> incomingPublicKey)
    {
        if (!owner || !freshLogin) return "sandbox_owner_reauthentication_required";
        if (!consent) return "sandbox_custody_consent_required";
        if (expectedRevision < 1 || expectedRevision != currentRevision) return "sandbox_memory_changed";
        return CryptographicOperations.FixedTimeEquals(registeredPublicKey, incomingPublicKey)
            ? null : "sandbox_key_pair_mismatch";
    }
}
