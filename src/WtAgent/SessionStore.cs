using System.Text.Json;

namespace WtAgent;

internal sealed class SessionStore
{
    private readonly JsonSerializerOptions _jsonOptions;

    public SessionStore(JsonSerializerOptions jsonOptions)
    {
        _jsonOptions = jsonOptions;
    }

    public async Task SaveAsync(string manifestPath, SessionManifest manifest)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, _jsonOptions));
    }

    public async Task<SessionManifest?> LoadAsync(string manifestPath)
    {
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(manifestPath);
        return await JsonSerializer.DeserializeAsync<SessionManifest>(stream, _jsonOptions);
    }
}
