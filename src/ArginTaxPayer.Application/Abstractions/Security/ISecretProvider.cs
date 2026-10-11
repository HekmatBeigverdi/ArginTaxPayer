namespace ArginTaxPayer.Application.Abstractions.Security;

/// <summary>
/// Retrieve a secret by its logical name. Never log or serialize the returned secret.
/// Secret values must not be placed in committed configuration files.
/// </summary>
public interface ISecretProvider
{
    SecretValue GetRequired(string name);
    SecretValue? GetOptional(string name);
}

/// <summary>
/// A deliberately redacted wrapper. Reveal() must only be called at a trusted
/// integration boundary, never in logging, exception or telemetry messages.
/// </summary>
public sealed class SecretValue
{
    private readonly string _value;

    public SecretValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException("A secret cannot be empty.", nameof(value));

        _value = value;
    }

    public string Reveal() => _value;

    public override string ToString() => "[REDACTED]";
}
