using System.Text.RegularExpressions;

namespace ArginTaxPayer.Infrastructure.Signing.Protection;

internal sealed record VaultProtectionOptions(
    Uri Address, string TransitMount, string TransitKey, string KvMount,
    string AgentTokenFile, string ServerVersion)
{
    // Review baseline, not a claim that deployment/security/restore acceptance has passed.
    public const string ReviewedServerVersion = "2.2.0";
    public void Validate()
    {
        if (Address is null || !Address.IsAbsoluteUri || Address.Scheme != Uri.UriSchemeHttps ||
            Address.UserInfo.Length != 0 || Address.AbsolutePath != "/" ||
            Address.Query.Length != 0 || Address.Fragment.Length != 0 ||
            ServerVersion != ReviewedServerVersion ||
            !Segment(TransitMount) || !Segment(TransitKey) || !Segment(KvMount) ||
            string.IsNullOrWhiteSpace(AgentTokenFile) || !Path.IsPathFullyQualified(AgentTokenFile))
            throw new KeyProtectionException(KeyProtectionFailure.Unavailable);
    }

    private static bool Segment(string? value) => value is { Length: > 0 and <= 64 } &&
        Regex.IsMatch(value, "^[a-zA-Z0-9][a-zA-Z0-9_-]*$", RegexOptions.CultureInvariant);
}

internal interface IVaultWorkloadTokenSource
{
    ValueTask<string> ReadAsync(CancellationToken ct);
}

// A Vault Agent writes/renews a restricted workload token. No token/root credential in appsettings.
internal sealed class VaultAgentTokenSource(string path) : IVaultWorkloadTokenSource
{
    public async ValueTask<string> ReadAsync(CancellationToken ct)
    {
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.LinkTarget is not null || file.Length is < 1 or > 4096)
                throw new KeyProtectionException(KeyProtectionFailure.Unavailable);
            if (!OperatingSystem.IsWindows())
            {
                var forbidden = UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
                if ((File.GetUnixFileMode(path) & forbidden) != 0)
                    throw new KeyProtectionException(KeyProtectionFailure.Unavailable);
            }
            var token = (await File.ReadAllTextAsync(path, ct)).Trim();
            if (token.Length is < 1 or > 4096 || token.Any(c => c < 33 || c > 126))
                throw new KeyProtectionException(KeyProtectionFailure.Unavailable);
            return token;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { throw new KeyProtectionException(KeyProtectionFailure.Unavailable); }
    }
}
