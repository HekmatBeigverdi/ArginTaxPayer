using System.Text.Json;

namespace ArginTaxPayer.Workers.DatabaseBackup;

public sealed record StoredFtpsCredentials(string Username, string Password);

/// <summary>Stores FTPS secrets outside settings JSON with owner-only file permissions.</summary>
public sealed class FtpsCredentialStore(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string filePath = Path.GetFullPath(path);

    public bool IsConfigured => File.Exists(filePath);

    public async Task SaveAsync(string username, string password, CancellationToken cancellationToken)
    {
        var credentials = new FtpsConnectionSettings("localhost", 21, username, password);
        credentials.Validate();
        var directory = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(directory);
        var temporary = filePath + ".partial-" + Guid.NewGuid().ToString("N");
        try
        {
            await using (var output = new FileStream(temporary, new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = 4096,
                Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
                UnixCreateMode = PrivateFileMode
            }))
            {
                await JsonSerializer.SerializeAsync(output, new StoredFtpsCredentials(username, password), JsonOptions,
                    cancellationToken);
                await output.FlushAsync(cancellationToken);
                output.Flush(true);
            }
            File.Move(temporary, filePath, true);
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                File.SetUnixFileMode(filePath, PrivateFileMode);
        }
        catch
        {
            try { File.Delete(temporary); } catch (IOException) { }
            throw;
        }
    }

    public async Task<StoredFtpsCredentials?> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath)) return null;
        if ((File.GetAttributes(filePath) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("FTPS credential file must not be a symbolic link.");
        await using var input = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await JsonSerializer.DeserializeAsync<StoredFtpsCredentials>(input, JsonOptions, cancellationToken);
    }

    private static readonly UnixFileMode PrivateFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
}
