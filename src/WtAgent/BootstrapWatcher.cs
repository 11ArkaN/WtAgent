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

    public static async Task<SessionPromptState?> ReadPromptStateAsync(string promptStateFilePath)
    {
        if (!File.Exists(promptStateFilePath))
        {
            return null;
        }

        for (var attempt = 0; attempt < 6; attempt++)
        {
            try
            {
                await using var stream = File.OpenRead(promptStateFilePath);
                return await JsonSerializer.DeserializeAsync<SessionPromptState>(stream);
            }
            catch (IOException)
            {
                await Task.Delay(60);
            }
        }

        return null;
    }

    public static async Task<SessionPromptState> WaitForPromptAdvanceAsync(string promptStateFilePath, int previousPromptSerial, TimeSpan timeout)
    {
        var started = DateTimeOffset.UtcNow;
        while (DateTimeOffset.UtcNow - started < timeout)
        {
            var state = await ReadPromptStateAsync(promptStateFilePath);
            if (state is not null && state.PromptSerial > previousPromptSerial)
            {
                return state;
            }

            await Task.Delay(150);
        }

        throw new TimeoutException($"Timed out waiting for prompt advance in '{promptStateFilePath}'.");
    }

    private sealed record DonePayload
    {
        [JsonPropertyName("exitCode")]
        public int ExitCode { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }
    }
}
