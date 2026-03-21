using System.Text.Json;

namespace WtAgent;

internal sealed class RegistryStore
{
    private readonly string _registryPath;
    private readonly string _lockPath;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public RegistryStore(string stateDirectory, JsonSerializerOptions jsonSerializerOptions)
    {
        _registryPath = Path.Combine(stateDirectory, "registry.json");
        _lockPath = Path.Combine(stateDirectory, "registry.lock");
        _jsonSerializerOptions = jsonSerializerOptions;
        Directory.CreateDirectory(stateDirectory);
    }

    public async Task UpsertAsync(RegistryRun run)
    {
        await _gate.WaitAsync();
        await using FileStream lockHandle = await AcquireFileLockAsync();
        try
        {
            var state = await LoadAsync();
            var existing = state.Runs.FindIndex(item => item.RunId == run.RunId);
            if (existing >= 0)
            {
                state.Runs[existing] = run;
            }
            else
            {
                state.Runs.Add(run);
            }

            await SaveAsync(state);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<RegistryRun?> GetAsync(string runId)
    {
        await _gate.WaitAsync();
        await using FileStream lockHandle = await AcquireFileLockAsync();
        try
        {
            var state = await LoadAsync();
            return state.Runs.SingleOrDefault(item => item.RunId.Equals(runId, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<int> CleanupAsync(string runsDirectory, TimeSpan olderThan)
    {
        await _gate.WaitAsync();
        await using FileStream lockHandle = await AcquireFileLockAsync();
        try
        {
            var state = await LoadAsync();
            var cutoff = DateTimeOffset.UtcNow - olderThan;
            var toRemove = state.Runs.Where(run => run.CompletedAtUtc is { } completed && completed < cutoff).ToList();

            foreach (var run in toRemove)
            {
                var runDirectory = Path.Combine(runsDirectory, run.RunId);
                if (Directory.Exists(runDirectory))
                {
                    Directory.Delete(runDirectory, true);
                }
            }

            state.Runs.RemoveAll(run => toRemove.Any(removed => removed.RunId == run.RunId));
            await SaveAsync(state);
            return toRemove.Count;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<FileStream> AcquireFileLockAsync()
    {
        var started = DateTimeOffset.UtcNow;
        while (DateTimeOffset.UtcNow - started < TimeSpan.FromSeconds(15))
        {
            try
            {
                return new FileStream(_lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                await Task.Delay(100);
            }
        }

        throw new IOException("Timed out waiting for the wt-agent registry lock.");
    }

    private async Task<RegistryState> LoadAsync()
    {
        if (!File.Exists(_registryPath))
        {
            return new RegistryState();
        }

        await using var stream = File.OpenRead(_registryPath);
        return await JsonSerializer.DeserializeAsync<RegistryState>(stream, _jsonSerializerOptions) ?? new RegistryState();
    }

    private async Task SaveAsync(RegistryState state)
    {
        await using var stream = File.Create(_registryPath);
        await JsonSerializer.SerializeAsync(stream, state, _jsonSerializerOptions);
    }
}
