namespace ArginTaxPayer.Domain.Taxpayers;

/// <summary>
/// Syntactic identifier checks only; authoritative registration is verified
/// by the official connector in Phase 10, never inferred from this validator.
/// </summary>
public static class FiscalMemoryOnboardingPolicy
{
    public static bool CanRegisterEnvironment(
        IReadOnlyCollection<FiscalConnectionEnvironment?> existing, FiscalConnectionEnvironment requested)
    {
        if (!Enum.IsDefined(requested)) return false;
        // An old, unclassified memory may already be a Production binding, so it
        // occupies that slot until explicitly classified. Sandbox is independent.
        return !existing.Any(environment => environment == requested ||
            requested == FiscalConnectionEnvironment.Production && environment is null);
    }

    public static bool TryNormalizeIdentifier(string? input, out string normalized)
    {
        normalized = input?.Trim().ToUpperInvariant() ?? string.Empty;
        return normalized.Length is >= 1 and <= 64 &&
            normalized.All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c));
    }

    /// <summary>
    /// Phase 06 cannot authorize replacement or activation without subscription
    /// binding state and a positive tax-authority connectivity result.
    /// </summary>
    public static bool CanLocallyRetireNeverActivatedMemory(
        FiscalMemoryStatus current, FiscalMemoryStatus target) =>
        current == FiscalMemoryStatus.PendingVerification &&
        target == FiscalMemoryStatus.Retired;
}
