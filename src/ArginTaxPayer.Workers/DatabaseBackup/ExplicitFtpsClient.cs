using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using FluentFTP;

namespace ArginTaxPayer.Workers.DatabaseBackup;

public sealed record FtpsConnectionSettings(string Host, int Port, string Username, string Password)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host) || Host.Contains('/') || Host.Contains('\\') ||
            Host.Contains('\r') || Host.Contains('\n')) throw new ArgumentException("Invalid FTPS host.");
        if (Port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(Port));
        if (string.IsNullOrWhiteSpace(Username) || Username.Contains('\r') || Username.Contains('\n'))
            throw new ArgumentException("Invalid FTPS username.");
        if (Password is null || Password.Contains('\r') || Password.Contains('\n'))
            throw new ArgumentException("Invalid FTPS password.");
    }
}

/// <summary>Explicit TLS FTPS transport. Every operation creates a fresh connection and fails closed on certificate errors.</summary>
public sealed class ExplicitFtpsClient(FtpsConnectionSettings settings) : IRemoteBackupTransport
{
    private const int MaxAttempts = 3;

    public async Task UploadAtomicallyAsync(string localPath, string remotePath, CancellationToken cancellationToken)
    {
        ValidateRemotePath(remotePath);
        if (!File.Exists(localPath)) throw new FileNotFoundException("Backup transfer source was not found.", localPath);
        var partial = remotePath + ".partial-" + Guid.NewGuid().ToString("N");
        try
        {
            await RetryAsync(async client =>
            {
                var status = await client.UploadFile(localPath, partial, FtpRemoteExists.Overwrite,
                    createRemoteDir: true, verifyOptions: FtpVerify.Retry, token: cancellationToken);
                if (status != FtpStatus.Success) throw new IOException("FTPS upload did not complete successfully.");
                await client.Rename(partial, remotePath, cancellationToken);
            }, cancellationToken);
        }
        catch
        {
            try { await RetryAsync(client => client.DeleteFile(partial, CancellationToken.None), CancellationToken.None); }
            catch { }
            throw;
        }
    }

    public async Task<IReadOnlyList<RemoteBackupMetadata>> ListAsync(string directory, CancellationToken cancellationToken)
    {
        ValidateRemotePath(directory);
        return await RetryAsync(async client =>
        {
            var listing = await client.GetListing(directory, cancellationToken);
            return (IReadOnlyList<RemoteBackupMetadata>)listing.Select(x => new RemoteBackupMetadata(
                x.Name, x.Modified.Kind == DateTimeKind.Utc ? x.Modified : x.Modified.ToUniversalTime(), x.Size,
                x.Type == FtpObjectType.File)).ToArray();
        }, cancellationToken);
    }

    public async Task DownloadAsync(string remotePath, string localPath, CancellationToken cancellationToken)
    {
        ValidateRemotePath(remotePath);
        await RetryAsync(async client =>
        {
            var status = await client.DownloadFile(localPath, remotePath, FtpLocalExists.Overwrite,
                FtpVerify.Retry, token: cancellationToken);
            if (status != FtpStatus.Success) throw new IOException("FTPS download did not complete successfully.");
        }, cancellationToken);
    }

    public async Task DeleteAsync(string remotePath, CancellationToken cancellationToken)
    {
        ValidateRemotePath(remotePath);
        await RetryAsync(client => client.DeleteFile(remotePath, cancellationToken), cancellationToken);
    }

    private async Task<T> RetryAsync<T>(Func<AsyncFtpClient, Task<T>> action, CancellationToken cancellationToken)
    {
        settings.Validate();
        Exception? last = null;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var client = CreateClient();
            try
            {
                client.ValidateCertificate += (_, e) => e.Accept = AcceptCertificate(e.PolicyErrors);
                await client.Connect(cancellationToken);
                if (!client.IsEncrypted) throw new AuthenticationException("FTPS server did not negotiate TLS.");
                return await action(client);
            }
            catch (Exception ex) when (attempt < MaxAttempts && ex is not OperationCanceledException)
            {
                last = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken);
            }
        }
        throw new IOException("FTPS operation failed after bounded retries.", last);
    }

    public static bool AcceptCertificate(SslPolicyErrors errors) => errors == SslPolicyErrors.None;

    public static FtpConfig CreateConfiguration() => new()
        {
            EncryptionMode = FtpEncryptionMode.Explicit,
            DataConnectionEncryption = true,
            DataConnectionType = FtpDataConnectionType.PASV,
            SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            ValidateAnyCertificate = false,
            ConnectTimeout = 15000,
            ReadTimeout = 30000,
            DataConnectionConnectTimeout = 15000,
            DataConnectionReadTimeout = 30000
        };

    private AsyncFtpClient CreateClient() =>
        new(settings.Host, settings.Username, settings.Password, settings.Port, CreateConfiguration());

    private static void ValidateRemotePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith('/') || path.Contains('\\') ||
            path.Contains('\r') || path.Contains('\n') || path.Split('/').Any(x => x is "." or ".."))
            throw new ArgumentException("Invalid remote FTPS path.", nameof(path));
    }

    private async Task RetryAsync(Func<AsyncFtpClient, Task> action, CancellationToken cancellationToken) =>
        await RetryAsync(async client => { await action(client); return true; }, cancellationToken);
}
