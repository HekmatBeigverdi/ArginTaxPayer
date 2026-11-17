using System.Text.Json;

namespace ArginTaxPayer.Workers.DatabaseBackup;

public sealed record ManualServiceStopRecord(bool RestartRequired, Guid? OperationId, DateTime UpdatedAtUtc);

/// <summary>Persists the manual API stop independently of the latest operation row.</summary>
public sealed class ManualServiceStopState(string stateDirectory)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    private readonly string path = Path.Combine(Path.GetFullPath(stateDirectory), "manual-service-stop.json");

    public async Task<ManualServiceStopRecord> GetAsync(CancellationToken ct)
    {
        if (!File.Exists(path)) return new(false, null, DateTime.UnixEpoch);
        await using var input = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<ManualServiceStopRecord>(input, Options, ct)
            ?? new(false, null, DateTime.UnixEpoch);
    }

    public async Task MarkRestartRequiredAsync(Guid operationId, CancellationToken ct)
    {
        await WriteAsync(new(true, operationId, DateTime.UtcNow), ct);
    }

    public async Task ClearAsync(CancellationToken ct) =>
        await WriteAsync(new(false, null, DateTime.UtcNow), ct);

    private async Task WriteAsync(ManualServiceStopRecord state, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".partial";
        await using (var output = new FileStream(temporary, new FileStreamOptions
        {
            Mode = FileMode.Create, Access = FileAccess.Write, Share = FileShare.None,
            BufferSize = 4096, Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
        }))
        {
            await JsonSerializer.SerializeAsync(output, state, Options, ct);
            await output.FlushAsync(ct); output.Flush(true);
        }
        File.Move(temporary, path, true);
    }
}
