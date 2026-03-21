using System.Text.Json.Serialization;

namespace WtAgent;

public sealed record StartSessionArguments(
    string Profile,
    string WorkingDirectory,
    int Columns,
    int Rows,
    string? ArtifactsDirectory);

public sealed record SessionSendArguments(
    string SessionId,
    string Input,
    SessionWaitMode WaitMode,
    int TimeoutSeconds,
    int PostWaitMs,
    bool CaptureAfterSend,
    bool Submit,
    string? ArtifactsDirectory);

public sealed record SessionStatusArguments(
    string SessionId,
    int TailLines,
    string? ArtifactsDirectory);

public sealed record SessionCaptureArguments(
    string SessionId,
    string? Label,
    int PostWaitMs,
    string? ArtifactsDirectory);

public sealed record SessionInterruptArguments(
    string SessionId,
    int PostWaitMs,
    bool CaptureAfterInterrupt,
    string? ArtifactsDirectory);

public sealed record SessionSubmitArguments(
    string SessionId,
    SessionWaitMode WaitMode,
    int TimeoutSeconds,
    int PostWaitMs,
    bool CaptureAfterSubmit,
    string? ArtifactsDirectory);

public sealed record SessionStopArguments(
    string SessionId,
    bool Interrupt,
    int PostWaitMs,
    string? ArtifactsDirectory);

public enum SessionWaitMode
{
    Prompt,
    None
}

public sealed record SessionArtifacts
{
    [JsonPropertyName("sessionPath")]
    public required string SessionPath { get; init; }

    [JsonPropertyName("transcriptPath")]
    public required string TranscriptPath { get; init; }

    [JsonPropertyName("capturesPath")]
    public required string CapturesPath { get; init; }

    [JsonPropertyName("latestCapturePath")]
    public string? LatestCapturePath { get; init; }

    [JsonPropertyName("promptStatePath")]
    public required string PromptStatePath { get; init; }

    [JsonPropertyName("sessionStatePath")]
    public required string SessionStatePath { get; init; }
}

public sealed record SessionPromptState
{
    [JsonPropertyName("promptSerial")]
    public int PromptSerial { get; init; }

    [JsonPropertyName("lastExitCode")]
    public int? LastExitCode { get; init; }

    [JsonPropertyName("cwd")]
    public string? CurrentWorkingDirectory { get; init; }

    [JsonPropertyName("prompt")]
    public string? Prompt { get; init; }

    [JsonPropertyName("updatedAtUtc")]
    public DateTimeOffset? UpdatedAtUtc { get; init; }
}

public sealed record SessionLiveInfo
{
    [JsonPropertyName("windowAlive")]
    public bool WindowAlive { get; init; }

    [JsonPropertyName("commandCount")]
    public int CommandCount { get; init; }

    [JsonPropertyName("lastInput")]
    public string? LastInput { get; init; }

    [JsonPropertyName("lastCaptureLabel")]
    public string? LastCaptureLabel { get; init; }

    [JsonPropertyName("promptState")]
    public SessionPromptState? PromptState { get; init; }

    [JsonPropertyName("transcriptTail")]
    public IReadOnlyList<string> TranscriptTail { get; init; } = [];

    [JsonPropertyName("updatedAtUtc")]
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed record SessionResult
{
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("window")]
    public required WindowInfo Window { get; init; }

    [JsonPropertyName("artifacts")]
    public required SessionArtifacts Artifacts { get; init; }

    [JsonPropertyName("live")]
    public required SessionLiveInfo Live { get; init; }

    [JsonPropertyName("startedAtUtc")]
    public required DateTimeOffset StartedAtUtc { get; init; }

    [JsonPropertyName("completedAtUtc")]
    public DateTimeOffset? CompletedAtUtc { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

internal sealed record SessionLayout
{
    public required string RootDirectory { get; init; }
    public required string StateDirectory { get; init; }
    public required string SessionsDirectory { get; init; }
    public required string SessionDirectory { get; init; }
    public required string SessionId { get; init; }
    public required string BootstrapScriptPath { get; init; }
    public required string ReadyFilePath { get; init; }
    public required string PromptStateFilePath { get; init; }
    public required string ManifestPath { get; init; }
    public required string CapturesDirectory { get; init; }
    public required string TranscriptPath { get; init; }
}

internal sealed record SessionManifest
{
    public required string SessionId { get; init; }
    public required string Status { get; set; }
    public required string Profile { get; init; }
    public required string WorkingDirectory { get; init; }
    public required string WindowTitle { get; init; }
    public int? Pid { get; set; }
    public string? Hwnd { get; set; }
    public required DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public int CommandCount { get; set; }
    public string? LastInput { get; set; }
    public string? LastCapturePath { get; set; }
    public string? LastCaptureLabel { get; set; }
    public required SessionArtifacts Artifacts { get; set; }
}
