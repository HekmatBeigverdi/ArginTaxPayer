namespace ArginTaxPayer.Domain.Signing;

/// <summary>Pure lifecycle rules; persistence must apply them with a revision CAS.</summary>
public static class SigningKeyLifecyclePolicy
{
    public const short Staged = 1;
    public const short LocallyReady = 2;
    public const short Suspended = 3;
    public const short Compromised = 4;
    public const short Superseded = 5;

    public static bool CanTransition(short from, short to) => (from, to) switch
    {
        (Staged, LocallyReady) => true,
        (LocallyReady, Suspended or Compromised or Superseded) => true,
        (Suspended, LocallyReady or Compromised or Superseded) => true,
        _ => false
    };

    public static bool CanSign(short lifecycle) => lifecycle == LocallyReady;
    public static bool CanSelect(short lifecycle) => lifecycle == LocallyReady;
    public static bool IsTerminal(short lifecycle) => lifecycle is Compromised or Superseded;
}
