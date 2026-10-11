using ArginTaxPayer.Application.Abstractions.Security;

namespace ArginTaxPayer.Infrastructure.Security;

/// <summary>
/// Bootstrap adapter for environment-injected secrets. Does not read files,
/// retrieve secrets from databases or emit actual values in errors/logs.
/// Replace this adapter with a managed vault adapter before production deployment.
/// </summary>
public sealed class EnvironmentSecretProvider(Func<string, string?> readEnvironment) : ISecretProvider
{
    public const string Prefix = "ARGIN_SECRET_";

    public SecretValue GetRequired(string name) =>
        GetOptional(name) ?? throw new InvalidOperationException(
            "The required application secret is not configured.");

    public SecretValue? GetOptional(string name)
    {
        var variableName = EnvironmentVariableName(name);
        var value = readEnvironment(variableName);
        return string.IsNullOrEmpty(value) ? null : new SecretValue(value);
    }

    public static string EnvironmentVariableName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > 64 ||
            name.Any(c => !(c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_')))
        {
            throw new ArgumentException(
                "Secret names must contain 1-64 uppercase ASCII letters, digits or underscores.",
                nameof(name));
        }

        return Prefix + name;
    }
}
