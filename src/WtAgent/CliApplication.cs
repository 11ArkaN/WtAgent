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
            return WriteError("launch_error", "Missing command. Expected one of: run, inspect, cleanup, session-start, session-send, session-status, session-capture, session-submit, session-interrupt, session-stop.");
        }

        return args[0].ToLowerInvariant() switch
        {
            "run" => await RunCommandAsync(args[1..]),
            "inspect" => await InspectCommandAsync(args[1..]),
            "cleanup" => await CleanupCommandAsync(args[1..]),
            "session-start" => await SessionStartCommandAsync(args[1..]),
            "session-send" => await SessionSendCommandAsync(args[1..]),
            "session-status" => await SessionStatusCommandAsync(args[1..]),
            "session-capture" => await SessionCaptureCommandAsync(args[1..]),
            "session-submit" => await SessionSubmitCommandAsync(args[1..]),
            "session-interrupt" => await SessionInterruptCommandAsync(args[1..]),
            "session-stop" => await SessionStopCommandAsync(args[1..]),
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

    private async Task<int> SessionStartCommandAsync(string[] args)
    {
        var parsed = ArgumentParser.ParseSessionStart(args);
        if (!parsed.Success)
        {
            return WriteSessionError("error", parsed.ErrorMessage!);
        }

        var manager = new WindowsTerminalSessionManager(_serializerOptions);
        var result = await manager.StartAsync(parsed.Arguments!);
        Console.WriteLine(JsonSerializer.Serialize(result, _serializerOptions));
        return result.Status is "idle" or "interactive" ? 0 : 1;
    }

    private async Task<int> SessionSendCommandAsync(string[] args)
    {
        var parsed = ArgumentParser.ParseSessionSend(args);
        if (!parsed.Success)
        {
            return WriteSessionError("error", parsed.ErrorMessage!);
        }

        var manager = new WindowsTerminalSessionManager(_serializerOptions);
        var result = await manager.SendAsync(parsed.Arguments!);
        Console.WriteLine(JsonSerializer.Serialize(result, _serializerOptions));
        return result.Status == "not_found" || result.Status == "error" ? 1 : 0;
    }

    private async Task<int> SessionStatusCommandAsync(string[] args)
    {
        var parsed = ArgumentParser.ParseSessionStatus(args);
        if (!parsed.Success)
        {
            return WriteSessionError("error", parsed.ErrorMessage!);
        }

        var manager = new WindowsTerminalSessionManager(_serializerOptions);
        var result = await manager.StatusAsync(parsed.Arguments!);
        Console.WriteLine(JsonSerializer.Serialize(result, _serializerOptions));
        return result.Status == "not_found" ? 1 : 0;
    }

    private async Task<int> SessionCaptureCommandAsync(string[] args)
    {
        var parsed = ArgumentParser.ParseSessionCapture(args);
        if (!parsed.Success)
        {
            return WriteSessionError("error", parsed.ErrorMessage!);
        }

        var manager = new WindowsTerminalSessionManager(_serializerOptions);
        var result = await manager.CaptureAsync(parsed.Arguments!);
        Console.WriteLine(JsonSerializer.Serialize(result, _serializerOptions));
        return result.Status == "not_found" ? 1 : 0;
    }

    private async Task<int> SessionInterruptCommandAsync(string[] args)
    {
        var parsed = ArgumentParser.ParseSessionInterrupt(args);
        if (!parsed.Success)
        {
            return WriteSessionError("error", parsed.ErrorMessage!);
        }

        var manager = new WindowsTerminalSessionManager(_serializerOptions);
        var result = await manager.InterruptAsync(parsed.Arguments!);
        Console.WriteLine(JsonSerializer.Serialize(result, _serializerOptions));
        return result.Status == "not_found" ? 1 : 0;
    }

    private async Task<int> SessionSubmitCommandAsync(string[] args)
    {
        var parsed = ArgumentParser.ParseSessionSubmit(args);
        if (!parsed.Success)
        {
            return WriteSessionError("error", parsed.ErrorMessage!);
        }

        var manager = new WindowsTerminalSessionManager(_serializerOptions);
        var result = await manager.SubmitAsync(parsed.Arguments!);
        Console.WriteLine(JsonSerializer.Serialize(result, _serializerOptions));
        return result.Status == "not_found" ? 1 : 0;
    }

    private async Task<int> SessionStopCommandAsync(string[] args)
    {
        var parsed = ArgumentParser.ParseSessionStop(args);
        if (!parsed.Success)
        {
            return WriteSessionError("error", parsed.ErrorMessage!);
        }

        var manager = new WindowsTerminalSessionManager(_serializerOptions);
        var result = await manager.StopAsync(parsed.Arguments!);
        Console.WriteLine(JsonSerializer.Serialize(result, _serializerOptions));
        return result.Status == "not_found" ? 1 : 0;
    }

    private int WriteSessionError(string status, string message)
    {
        var errorResult = new SessionResult
        {
            SessionId = "n/a",
            Status = status,
            Window = new WindowInfo
            {
                Title = string.Empty,
                Profile = string.Empty,
                Pid = null,
                Hwnd = null
            },
            Artifacts = new SessionArtifacts
            {
                SessionPath = string.Empty,
                TranscriptPath = string.Empty,
                CapturesPath = string.Empty,
                LatestCapturePath = null,
                PromptStatePath = string.Empty,
                SessionStatePath = string.Empty
            },
            Live = new SessionLiveInfo
            {
                WindowAlive = false,
                CommandCount = 0,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            },
            StartedAtUtc = DateTimeOffset.UtcNow,
            Error = message
        };

        Console.WriteLine(JsonSerializer.Serialize(errorResult, _serializerOptions));
        return 1;
    }
}
