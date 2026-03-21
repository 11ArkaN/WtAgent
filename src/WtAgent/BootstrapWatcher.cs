using System.Text.Json;
using System.Text.Json.Serialization;

namespace WtAgent;

internal static class BootstrapWatcher
{
    public static async Task WaitForReadyAsync(string readyFilePath, TimeSpan timeout)
    {
        var started = DateTimeOffset.UtcNow;
        while (DateTimeOffset.UtcNow - started < timeout)
        {
            if (File.Exists(readyFilePath))
            {
                return;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Timed out waiting for '{readyFilePath}'.");
    }

    public static async Task<BootstrapResult> WaitForCompletionAsync(string doneFilePath, TimeSpan timeout)
    {
        var started = DateTimeOffset.UtcNow;

        while (DateTimeOffset.UtcNow - started < timeout)
        {
            if (File.Exists(doneFilePath))
            {
                await using var stream = File.OpenRead(doneFilePath);
                var payload = await JsonSerializer.DeserializeAsync<DonePayload>(stream);
                return new BootstrapResult(payload?.ExitCode ?? 1);
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Timed out waiting for '{doneFilePath}'.");
    }

    private sealed record DonePayload
    {
        [JsonPropertyName("exitCode")]
        public int ExitCode { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }
    }
}
