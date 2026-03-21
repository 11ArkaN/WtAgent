using System.Text.Json.Serialization;

namespace WtAgent;

public sealed record RunArguments(
    string Command,
    string Profile,
    string WorkingDirectory,
    ShellCommandMode ShellCommandMode,
    int TimeoutSeconds,
    int Columns,
    int Rows,
    string? ArtifactsDirectory);

public sealed record InspectArguments(string RunId, string? ArtifactsDirectory);

public sealed record CleanupArguments(int OlderThanHours, string? ArtifactsDirectory);

public enum ShellCommandMode
{
    Raw,
    PowerShell
}

public enum ShellFlavor
{
    Unknown,
    WindowsPowerShell,
    Pwsh,
    Cmd,
    Bash,
    Wsl
}

public sealed record AgentRunResult
{
    [JsonPropertyName("runId")]
    public required string RunId { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("exitCode")]
    public int? ExitCode { get; init; }

    [JsonPropertyName("artifacts")]
    public required ArtifactSet Artifacts { get; init; }

    [JsonPropertyName("window")]
    public required WindowInfo Window { get; init; }

    [JsonPropertyName("timing")]
    public required TimingInfo Timing { get; init; }

    [JsonPropertyName("capture")]
    public required CaptureInfo Capture { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

public sealed record ArtifactSet
{
    [JsonPropertyName("pngPath")]
    public required string PngPath { get; init; }

    [JsonPropertyName("stdoutPath")]
    public required string StdoutPath { get; init; }

    [JsonPropertyName("stderrPath")]
    public required string StderrPath { get; init; }

    [JsonPropertyName("transcriptPath")]
    public required string TranscriptPath { get; init; }

    [JsonPropertyName("resultPath")]
    public required string ResultPath { get; init; }

    [JsonPropertyName("metaPath")]
    public required string MetaPath { get; init; }
}

public sealed record WindowInfo
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("profile")]
    public required string Profile { get; init; }

    [JsonPropertyName("pid")]
    public int? Pid { get; init; }

    [JsonPropertyName("hwnd")]
    public string? Hwnd { get; init; }
}

public sealed record TimingInfo
{
    [JsonPropertyName("startedAtUtc")]
    public required DateTimeOffset StartedAtUtc { get; init; }

    [JsonPropertyName("completedAtUtc")]
    public DateTimeOffset? CompletedAtUtc { get; init; }

    [JsonPropertyName("durationMs")]
    public long? DurationMs { get; init; }
}

public sealed record CaptureInfo
{
    [JsonPropertyName("modeRequested")]
    public required string ModeRequested { get; init; }

    [JsonPropertyName("modeUsed")]
    public required string ModeUsed { get; init; }

    [JsonPropertyName("blankFrameDetected")]
    public bool BlankFrameDetected { get; init; }
}

public sealed record RegistryState
{
    [JsonPropertyName("runs")]
    public List<RegistryRun> Runs { get; init; } = [];
}

public sealed record RegistryRun
{
    [JsonPropertyName("runId")]
    public required string RunId { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; set; }

    [JsonPropertyName("profile")]
    public required string Profile { get; init; }

    [JsonPropertyName("windowTitle")]
    public required string WindowTitle { get; init; }

    [JsonPropertyName("pid")]
    public int? Pid { get; set; }

    [JsonPropertyName("hwnd")]
    public string? Hwnd { get; set; }

    [JsonPropertyName("artifacts")]
    public required ArtifactSet Artifacts { get; init; }

    [JsonPropertyName("startedAtUtc")]
    public required DateTimeOffset StartedAtUtc { get; init; }

    [JsonPropertyName("completedAtUtc")]
    public DateTimeOffset? CompletedAtUtc { get; set; }
}

public sealed record RunLayout
{
    public required string RootDirectory { get; init; }
    public required string StateDirectory { get; init; }
    public required string RunsDirectory { get; init; }
    public required string RunDirectory { get; init; }
    public required string RunId { get; init; }
    public required string BootstrapScriptPath { get; init; }
    public required string CommandFilePath { get; init; }
    public required string DoneFilePath { get; init; }
    public required string ResultFilePath { get; init; }
    public required string MetadataFilePath { get; init; }
    public required ArtifactSet Artifacts { get; init; }
}

public sealed record TerminalProfile
{
    public required string Name { get; init; }
    public string? Commandline { get; init; }
    public string? Source { get; init; }
    public ShellFlavor Flavor { get; init; }
}

internal sealed record LaunchResult(
    int ProcessId,
    IntPtr ProcessHandle,
    IntPtr ThreadHandle,
    string DesktopName);

internal sealed record WindowDiscoveryResult(
    IntPtr Hwnd,
    int ProcessId,
    string Title);

internal sealed record BootstrapResult(int ExitCode);

internal sealed record CaptureResult(string ModeUsed, bool BlankFrameDetected);
