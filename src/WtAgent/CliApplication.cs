using System.Text.Json;

namespace WtAgent;

internal sealed class CliApplication
{
    private readonly JsonSerializerOptions _serializerOptions;

    public CliApplication(JsonSerializerOptions serializerOptions)
    {
        _serializerOptions = serializerOptions;
    }

    public async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            return WriteError("launch_error", "Missing command. Expected one of: run, inspect, cleanup.");
        }

        return args[0].ToLowerInvariant() switch
        {
            "run" => await RunCommandAsync(args[1..]),
            "inspect" => await InspectCommandAsync(args[1..]),
            "cleanup" => await CleanupCommandAsync(args[1..]),
            _ => WriteError("launch_error", $"Unknown command '{args[0]}'.")
        };
    }

    private async Task<int> RunCommandAsync(string[] args)
    {
        var parsed = ArgumentParser.ParseRun(args);
        if (!parsed.Success)
        {
            return WriteError("launch_error", parsed.ErrorMessage!);
        }

        var runArguments = parsed.RunArguments!;
        var root = AgentPaths.ResolveRoot(runArguments.ArtifactsDirectory, runArguments.WorkingDirectory);
        var registry = new RegistryStore(root.StateDirectory, _serializerOptions);
        var layout = AgentPaths.CreateRunLayout(runArguments.ArtifactsDirectory, runArguments.WorkingDirectory);
        var terminalSettings = new WindowsTerminalSettings();
        var profile = terminalSettings.ResolveProfile(runArguments.Profile);
        var title = $"WT_AGENT_{layout.RunId}";
        var startedAtUtc = DateTimeOffset.UtcNow;

        var registryRun = new RegistryRun
        {
            RunId = layout.RunId,
            Status = "launching",
            Profile = profile.Name,
            WindowTitle = title,
            Artifacts = layout.Artifacts,
            StartedAtUtc = startedAtUtc
        };

        await registry.UpsertAsync(registryRun);

        var runner = new WindowsTerminalRunner(_serializerOptions, registry);
        var result = await runner.RunAsync(runArguments, layout, profile, title, startedAtUtc);
        Console.WriteLine(JsonSerializer.Serialize(result, _serializerOptions));
        return result.Status == "ok" ? 0 : 1;
    }

    private async Task<int> InspectCommandAsync(string[] args)
    {
        var parsed = ArgumentParser.ParseInspect(args);
        if (!parsed.Success)
        {
            return WriteError("launch_error", parsed.ErrorMessage!);
        }

        var root = AgentPaths.ResolveRoot(parsed.InspectArguments!.ArtifactsDirectory, Environment.CurrentDirectory);
        var registry = new RegistryStore(root.StateDirectory, _serializerOptions);
        var run = await registry.GetAsync(parsed.InspectArguments.RunId);
        if (run is null)
        {
            return WriteError("launch_error", $"Run '{parsed.InspectArguments.RunId}' was not found.");
        }

        Console.WriteLine(JsonSerializer.Serialize(RegistryProjector.ToResult(run), _serializerOptions));
        return 0;
    }

    private async Task<int> CleanupCommandAsync(string[] args)
    {
        var parsed = ArgumentParser.ParseCleanup(args);
        if (!parsed.Success)
        {
            return WriteError("launch_error", parsed.ErrorMessage!);
        }

        var root = AgentPaths.ResolveRoot(parsed.CleanupArguments!.ArtifactsDirectory, Environment.CurrentDirectory);
        var registry = new RegistryStore(root.StateDirectory, _serializerOptions);
        var removed = await registry.CleanupAsync(root.RunsDirectory, TimeSpan.FromHours(parsed.CleanupArguments.OlderThanHours));
        Console.WriteLine(JsonSerializer.Serialize(new { removed }, _serializerOptions));
        return 0;
    }

    private int WriteError(string status, string message)
    {
        var errorResult = new AgentRunResult
        {
            RunId = "n/a",
            Status = status,
            ExitCode = null,
            Error = message,
            Artifacts = new ArtifactSet
            {
                PngPath = string.Empty,
                StdoutPath = string.Empty,
                StderrPath = string.Empty,
                TranscriptPath = string.Empty,
                ResultPath = string.Empty,
                MetaPath = string.Empty
            },
            Window = new WindowInfo
            {
                Title = string.Empty,
                Profile = string.Empty,
                Pid = null,
                Hwnd = null
            },
            Timing = new TimingInfo
            {
                StartedAtUtc = DateTimeOffset.UtcNow,
                CompletedAtUtc = DateTimeOffset.UtcNow,
                DurationMs = 0
            },
            Capture = new CaptureInfo
            {
                ModeRequested = "auto",
                ModeUsed = "none",
                BlankFrameDetected = false
            }
        };

        Console.WriteLine(JsonSerializer.Serialize(errorResult, _serializerOptions));
        return 1;
    }
}
