using System.Text.Json;

namespace ArginTaxPayer.Workers.DatabaseBackup;

public sealed record RecoveryState(bool RecoveryMode, Guid? StageId, string? ActiveDatabase,
    string? PreviousDatabase, DateTime UpdatedAtUtc, string? Message);

public sealed class RecoveryModeStore(string stateDirectory)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    private readonly string path = Path.Combine(Path.GetFullPath(stateDirectory), "recovery-state.json");

    public async Task<RecoveryState> GetAsync(CancellationToken ct)
    {
        if (!File.Exists(path)) return new(false, null, null, null, DateTime.UnixEpoch, null);
        await using var input = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<RecoveryState>(input, Options, ct)
            ?? new(false, null, null, null, DateTime.UnixEpoch, null);
    }

    public async Task SaveAsync(RecoveryState state, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".partial";
        await using (var output = new FileStream(temporary, new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.None,
            BufferSize = 4096,
            Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
        }))
        {
            await JsonSerializer.SerializeAsync(output, state, Options, ct);
            await output.FlushAsync(ct);
            output.Flush(true);
        }
        File.Move(temporary, path, true);
    }
}
