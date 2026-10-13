using System.Text;
using Microsoft.Extensions.Configuration;

namespace ArginTaxPayer.Infrastructure.Persistence.Identity;

/// <summary>
/// Local-only, opt-in developer test transport. Writes the latest OTP into an
/// operator-owned file, never an HTTP response or application log.
/// The Development host is already constrained to loopback identity endpoints.
/// Do NOT register this implementation in Production.
/// </summary>
public sealed class LocalFileOtpSender(IConfiguration configuration) : IIdentityOtpSender
{
    public async Task<bool> SendAsync(string normalizedMobile, string code, string purpose, Guid deliveryId,
        CancellationToken cancellationToken = default)
    {
        if (configuration["Identity:OtpDeliveryMode"] != "LocalFile" ||
            !IranianMobileNumber.TryNormalize(normalizedMobile, out _) ||
            code is not { Length: 6 } || !code.All(char.IsAsciiDigit))
            return false;

        var path = configuration["Identity:OtpLocalFile"];
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            return false;

        var dir = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            return false;

        // CreateNew first, and refuse symlink destinations on subsequent writes.
        // Never write to repository-tracked source or world-readable temp artifacts.
        if (File.Exists(path))
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                return false;
        }
        else
        {
            await using var initial = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 1, FileOptions.None);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        var body = "LOCAL DEVELOPMENT OTP — DELETE AFTER TEST\n" +
            "Purpose-specific OTP expires in 3 minutes. Never share this file.\n" +
            "Mobile: " + normalizedMobile + "\n" +
            "Code: " + code + "\n" +
            "Delivery ID: " + deliveryId.ToString("N") + "\n";
        await File.WriteAllTextAsync(path, body, new UTF8Encoding(false), cancellationToken);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return true;
    }
}
