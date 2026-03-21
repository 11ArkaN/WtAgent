using System.Text.Json;

namespace WtAgent;

internal sealed class SessionStore
{
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SessionStore(JsonSerializerOptions jsonOptions)
    {
        _jsonOptions = jsonOptions;
    }

    public async Task SaveAsync(string manifestPath, SessionManifest manifest)
    {
        await _gate.WaitAsync();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
            await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, _jsonOptions));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SessionManifest?> LoadAsync(string manifestPath)
    {
        await _gate.WaitAsync();
        try
        {
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            await using var stream = File.OpenRead(manifestPath);
            return await JsonSerializer.DeserializeAsync<SessionManifest>(stream, _jsonOptions);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<SessionManifest>> ListAsync(string sessionsDirectory)
    {
        await _gate.WaitAsync();
        try
        {
            if (!Directory.Exists(sessionsDirectory))
            {
                return [];
            }

            var manifests = new List<SessionManifest>();
            foreach (var sessionDirectory in Directory.EnumerateDirectories(sessionsDirectory))
            {
                var manifestPath = Path.Combine(sessionDirectory, "session.json");
                if (!File.Exists(manifestPath))
                {
                    continue;
                }

                await using var stream = File.OpenRead(manifestPath);
                var manifest = await JsonSerializer.DeserializeAsync<SessionManifest>(stream, _jsonOptions);
                if (manifest is not null)
                {
                    manifests.Add(manifest);
                }
            }

            return manifests
                .OrderByDescending(item => item.StartedAtUtc)
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }
}
