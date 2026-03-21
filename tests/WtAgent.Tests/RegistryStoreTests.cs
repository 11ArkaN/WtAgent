using WtAgent;

namespace WtAgent.Tests;

public sealed class RegistryStoreTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "wt-agent-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task UpsertAndLoadRoundTripsRun()
    {
        Directory.CreateDirectory(_tempDir);
        var store = new RegistryStore(_tempDir, JsonDefaults.Create());
        var run = new RegistryRun
        {
            RunId = "run-1",
            Status = "ok",
            Profile = "Windows PowerShell",
            WindowTitle = "WT_AGENT_run-1",
            StartedAtUtc = DateTimeOffset.UtcNow,
            Artifacts = new ArtifactSet
            {
                PngPath = "a",
                StdoutPath = "b",
                StderrPath = "c",
                TranscriptPath = "d",
                ResultPath = "e",
                MetaPath = "f"
            }
        };

        await store.UpsertAsync(run);
        var loaded = await store.GetAsync("run-1");

        Assert.NotNull(loaded);
        Assert.Equal("ok", loaded!.Status);
        Assert.Equal("Windows PowerShell", loaded.Profile);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }
}
