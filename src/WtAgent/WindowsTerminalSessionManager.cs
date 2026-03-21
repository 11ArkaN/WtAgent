using System.Globalization;
using System.Text;
using System.Text.Json;

namespace WtAgent;

internal sealed class WindowsTerminalSessionManager
{
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SessionStore _sessionStore;
    private readonly WindowCaptureService _captureService = new();

    public WindowsTerminalSessionManager(JsonSerializerOptions jsonOptions)
    {
        _jsonOptions = jsonOptions;
        _sessionStore = new SessionStore(jsonOptions);
    }

    public async Task<SessionResult> StartAsync(StartSessionArguments arguments)
    {
        var layout = AgentPaths.CreateSessionLayout(arguments.ArtifactsDirectory, arguments.WorkingDirectory);
        var terminalSettings = new WindowsTerminalSettings();
        var profile = terminalSettings.ResolveProfile(arguments.Profile);
        var title = $"WT_AGENT_SESSION_{layout.SessionId}";
        var startedAtUtc = DateTimeOffset.UtcNow;

        var manifest = new SessionManifest
        {
            SessionId = layout.SessionId,
            Status = "starting",
            Profile = profile.Name,
            WorkingDirectory = arguments.WorkingDirectory,
            WindowTitle = title,
            StartedAtUtc = startedAtUtc,
            Artifacts = new SessionArtifacts
            {
                SessionPath = layout.SessionDirectory,
                TranscriptPath = layout.TranscriptPath,
                CapturesPath = layout.CapturesDirectory,
                LatestCapturePath = null,
                PromptStatePath = layout.PromptStateFilePath,
                SessionStatePath = layout.ManifestPath
            }
        };

        await _sessionStore.SaveAsync(layout.ManifestPath, manifest);

        LaunchResult? launch = null;
        WindowDiscoveryResult? window = null;

        try
        {
            SessionBootstrapScriptWriter.Write(layout, arguments, profile, title);
            launch = WindowsTerminalLauncher.LaunchSession(arguments, layout, profile, title);
            manifest.Pid = launch.ProcessId;
            await _sessionStore.SaveAsync(layout.ManifestPath, manifest);

            window = await WindowLocator.WaitForWindowAsync(title, launch.DesktopName, launch.ProcessId, TimeSpan.FromSeconds(20));
            manifest.Pid = window.ProcessId;
            manifest.Hwnd = ToHex(window.Hwnd);

            await BootstrapWatcher.WaitForReadyAsync(layout.ReadyFilePath, TimeSpan.FromSeconds(15));
            await Task.Delay(400);

            manifest.Status = "idle";
            manifest.LastCapturePath = CaptureToNextFile(layout.CapturesDirectory, "start", window.Hwnd);
            manifest.LastCaptureLabel = "start";
            manifest.Artifacts = manifest.Artifacts with { LatestCapturePath = manifest.LastCapturePath };
            await _sessionStore.SaveAsync(layout.ManifestPath, manifest);

            return await BuildSessionResultAsync(manifest, tailLines: 40, explicitWindowAlive: true);
        }
        catch (Exception ex)
        {
            manifest.Status = "error";
            manifest.CompletedAtUtc = DateTimeOffset.UtcNow;
            await _sessionStore.SaveAsync(layout.ManifestPath, manifest);

            if (window is not null)
            {
                NativeMethods.PostMessage(window.Hwnd, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }

            if (launch is not null && launch.ProcessHandle != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(launch.ProcessHandle);
            }

            if (launch is not null && launch.ThreadHandle != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(launch.ThreadHandle);
            }

            return await BuildSessionResultAsync(manifest, 40, explicitWindowAlive: false, error: ex.Message);
        }
    }

    public async Task<SessionResult> SendAsync(SessionSendArguments arguments)
    {
        var manifest = await LoadManifestAsync(arguments.ArtifactsDirectory, arguments.SessionId);
        if (manifest is null)
        {
            return CreateMissingSessionResult(arguments.SessionId);
        }

        var hwnd = ResolveWindowHandle(manifest);
        if (hwnd == IntPtr.Zero)
        {
            manifest.Status = "stopped";
            manifest.CompletedAtUtc ??= DateTimeOffset.UtcNow;
            await SaveManifestAsync(manifest);
            return await BuildSessionResultAsync(manifest, 40, explicitWindowAlive: false, error: "Session window is no longer available.");
        }

        var previousPromptSerial = (await BootstrapWatcher.ReadPromptStateAsync(manifest.Artifacts.PromptStatePath))?.PromptSerial ?? 0;

        manifest.Status = "busy";
        manifest.LastInput = arguments.Input;
        if (arguments.Submit)
        {
            manifest.CommandCount++;
        }

        await SaveManifestAsync(manifest);

        NativeMethods.PasteText(hwnd, arguments.Input);
        if (arguments.Submit)
        {
            await Task.Delay(140);
            NativeMethods.Submit(hwnd);
        }

        SessionPromptState? promptState = null;
        if (arguments.Submit && arguments.WaitMode == SessionWaitMode.Prompt)
        {
            promptState = await BootstrapWatcher.WaitForPromptAdvanceAsync(
                manifest.Artifacts.PromptStatePath,
                previousPromptSerial,
                TimeSpan.FromSeconds(arguments.TimeoutSeconds));
        }

        if (arguments.PostWaitMs > 0)
        {
            await Task.Delay(arguments.PostWaitMs);
        }

        if (arguments.CaptureAfterSend)
        {
            var label = arguments.Submit ? $"command-{manifest.CommandCount:D3}" : $"input-{manifest.CommandCount:D3}";
            manifest.LastCapturePath = CaptureToNextFile(manifest.Artifacts.CapturesPath, label, hwnd);
            manifest.LastCaptureLabel = label;
            manifest.Artifacts = manifest.Artifacts with { LatestCapturePath = manifest.LastCapturePath };
        }

        manifest.Status = arguments.Submit && arguments.WaitMode == SessionWaitMode.Prompt
            ? "idle"
            : "interactive";
        await SaveManifestAsync(manifest);

        return await BuildSessionResultAsync(manifest, 40, explicitWindowAlive: true, promptStateOverride: promptState);
    }

    public async Task<SessionResult> CaptureAsync(SessionCaptureArguments arguments)
    {
        var manifest = await LoadManifestAsync(arguments.ArtifactsDirectory, arguments.SessionId);
        if (manifest is null)
        {
            return CreateMissingSessionResult(arguments.SessionId);
        }

        var hwnd = ResolveWindowHandle(manifest);
        if (hwnd == IntPtr.Zero)
        {
            manifest.Status = "stopped";
            manifest.CompletedAtUtc ??= DateTimeOffset.UtcNow;
            await SaveManifestAsync(manifest);
            return await BuildSessionResultAsync(manifest, 40, explicitWindowAlive: false, error: "Session window is no longer available.");
        }

        if (arguments.PostWaitMs > 0)
        {
            await Task.Delay(arguments.PostWaitMs);
        }

        var label = string.IsNullOrWhiteSpace(arguments.Label)
            ? $"capture-{manifest.CommandCount:D3}"
            : SanitizeLabel(arguments.Label);

        manifest.LastCapturePath = CaptureToNextFile(manifest.Artifacts.CapturesPath, label, hwnd);
        manifest.LastCaptureLabel = label;
        manifest.Artifacts = manifest.Artifacts with { LatestCapturePath = manifest.LastCapturePath };
        await SaveManifestAsync(manifest);

        return await BuildSessionResultAsync(manifest, 40, explicitWindowAlive: true);
    }

    public async Task<SessionResult> InterruptAsync(SessionInterruptArguments arguments)
    {
        var manifest = await LoadManifestAsync(arguments.ArtifactsDirectory, arguments.SessionId);
        if (manifest is null)
        {
            return CreateMissingSessionResult(arguments.SessionId);
        }

        var hwnd = ResolveWindowHandle(manifest);
        if (hwnd == IntPtr.Zero)
        {
            manifest.Status = "stopped";
            manifest.CompletedAtUtc ??= DateTimeOffset.UtcNow;
            await SaveManifestAsync(manifest);
            return await BuildSessionResultAsync(manifest, 40, explicitWindowAlive: false, error: "Session window is no longer available.");
        }

        var previousPromptSerial = (await BootstrapWatcher.ReadPromptStateAsync(manifest.Artifacts.PromptStatePath))?.PromptSerial ?? 0;
        NativeMethods.SendCtrlC(hwnd);

        if (arguments.PostWaitMs > 0)
        {
            await Task.Delay(arguments.PostWaitMs);
        }

        var advanced = false;
        try
        {
            var state = await BootstrapWatcher.WaitForPromptAdvanceAsync(
                manifest.Artifacts.PromptStatePath,
                previousPromptSerial,
                TimeSpan.FromMilliseconds(Math.Max(300, arguments.PostWaitMs)));
            advanced = state.PromptSerial > previousPromptSerial;
        }
        catch (TimeoutException)
        {
        }

        if (arguments.CaptureAfterInterrupt)
        {
            manifest.LastCapturePath = CaptureToNextFile(manifest.Artifacts.CapturesPath, "interrupt", hwnd);
            manifest.LastCaptureLabel = "interrupt";
            manifest.Artifacts = manifest.Artifacts with { LatestCapturePath = manifest.LastCapturePath };
        }

        manifest.Status = advanced ? "idle" : "interrupted";
        await SaveManifestAsync(manifest);
        return await BuildSessionResultAsync(manifest, 40, explicitWindowAlive: true);
    }

    public async Task<SessionResult> SubmitAsync(SessionSubmitArguments arguments)
    {
        var manifest = await LoadManifestAsync(arguments.ArtifactsDirectory, arguments.SessionId);
        if (manifest is null)
        {
            return CreateMissingSessionResult(arguments.SessionId);
        }

        var hwnd = ResolveWindowHandle(manifest);
        if (hwnd == IntPtr.Zero)
        {
            manifest.Status = "stopped";
            manifest.CompletedAtUtc ??= DateTimeOffset.UtcNow;
            await SaveManifestAsync(manifest);
            return await BuildSessionResultAsync(manifest, 40, explicitWindowAlive: false, error: "Session window is no longer available.");
        }

        var previousPromptSerial = (await BootstrapWatcher.ReadPromptStateAsync(manifest.Artifacts.PromptStatePath))?.PromptSerial ?? 0;
        NativeMethods.Submit(hwnd);

        SessionPromptState? promptState = null;
        if (arguments.WaitMode == SessionWaitMode.Prompt)
        {
            promptState = await BootstrapWatcher.WaitForPromptAdvanceAsync(
                manifest.Artifacts.PromptStatePath,
                previousPromptSerial,
                TimeSpan.FromSeconds(arguments.TimeoutSeconds));
        }

        if (arguments.PostWaitMs > 0)
        {
            await Task.Delay(arguments.PostWaitMs);
        }

        if (arguments.CaptureAfterSubmit)
        {
            manifest.LastCapturePath = CaptureToNextFile(manifest.Artifacts.CapturesPath, $"submit-{manifest.CommandCount:D3}", hwnd);
            manifest.LastCaptureLabel = $"submit-{manifest.CommandCount:D3}";
            manifest.Artifacts = manifest.Artifacts with { LatestCapturePath = manifest.LastCapturePath };
        }

        manifest.Status = arguments.WaitMode == SessionWaitMode.Prompt ? "idle" : "interactive";
        await SaveManifestAsync(manifest);
        return await BuildSessionResultAsync(manifest, 40, explicitWindowAlive: true, promptStateOverride: promptState);
    }

    public async Task<SessionResult> StopAsync(SessionStopArguments arguments)
    {
        var manifest = await LoadManifestAsync(arguments.ArtifactsDirectory, arguments.SessionId);
        if (manifest is null)
        {
            return CreateMissingSessionResult(arguments.SessionId);
        }

        var hwnd = ResolveWindowHandle(manifest);
        if (hwnd != IntPtr.Zero)
        {
            if (arguments.Interrupt)
            {
                NativeMethods.SendCtrlC(hwnd);
                if (arguments.PostWaitMs > 0)
                {
                    await Task.Delay(arguments.PostWaitMs);
                }
            }

            NativeMethods.PostMessage(hwnd, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }

        manifest.Status = "stopped";
        manifest.CompletedAtUtc = DateTimeOffset.UtcNow;
        await SaveManifestAsync(manifest);
        return await BuildSessionResultAsync(manifest, 40, explicitWindowAlive: false);
    }

    public async Task<SessionResult> StatusAsync(SessionStatusArguments arguments)
    {
        var manifest = await LoadManifestAsync(arguments.ArtifactsDirectory, arguments.SessionId);
        if (manifest is null)
        {
            return CreateMissingSessionResult(arguments.SessionId);
        }

        var hwnd = ResolveWindowHandle(manifest);
        var alive = hwnd != IntPtr.Zero;
        if (!alive && manifest.CompletedAtUtc is null)
        {
            manifest.Status = "stopped";
            manifest.CompletedAtUtc = DateTimeOffset.UtcNow;
            await SaveManifestAsync(manifest);
        }

        return await BuildSessionResultAsync(manifest, arguments.TailLines, explicitWindowAlive: alive);
    }

    private async Task<SessionManifest?> LoadManifestAsync(string? artifactsDirectory, string sessionId)
    {
        var manifestPath = AgentPaths.ResolveSessionManifestPath(artifactsDirectory, Environment.CurrentDirectory, sessionId);
        return await _sessionStore.LoadAsync(manifestPath);
    }

    private async Task SaveManifestAsync(SessionManifest manifest)
    {
        await _sessionStore.SaveAsync(manifest.Artifacts.SessionStatePath, manifest);
    }

    private async Task<SessionResult> BuildSessionResultAsync(
        SessionManifest manifest,
        int tailLines,
        bool explicitWindowAlive,
        string? error = null,
        SessionPromptState? promptStateOverride = null)
    {
        var promptState = promptStateOverride ?? await BootstrapWatcher.ReadPromptStateAsync(manifest.Artifacts.PromptStatePath);
        return new SessionResult
        {
            SessionId = manifest.SessionId,
            Status = manifest.Status,
            Window = new WindowInfo
            {
                Title = manifest.WindowTitle,
                Profile = manifest.Profile,
                Pid = manifest.Pid,
                Hwnd = manifest.Hwnd
            },
            Artifacts = manifest.Artifacts,
            Live = new SessionLiveInfo
            {
                WindowAlive = explicitWindowAlive,
                CommandCount = manifest.CommandCount,
                LastInput = manifest.LastInput,
                LastCaptureLabel = manifest.LastCaptureLabel,
                PromptState = promptState,
                TranscriptTail = ReadTranscriptTail(manifest.Artifacts.TranscriptPath, tailLines),
                UpdatedAtUtc = DateTimeOffset.UtcNow
            },
            StartedAtUtc = manifest.StartedAtUtc,
            CompletedAtUtc = manifest.CompletedAtUtc,
            Error = error
        };
    }

    private SessionResult CreateMissingSessionResult(string sessionId)
    {
        return new SessionResult
        {
            SessionId = sessionId,
            Status = "not_found",
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
            Error = "Session was not found."
        };
    }

    private IntPtr ResolveWindowHandle(SessionManifest manifest)
    {
        if (!string.IsNullOrWhiteSpace(manifest.Hwnd) && TryParseHwnd(manifest.Hwnd, out var hwnd) && NativeMethods.IsWindow(hwnd))
        {
            return hwnd;
        }

        var discovered = NativeMethods.FindWindowByTitle(manifest.WindowTitle);
        if (discovered != IntPtr.Zero)
        {
            manifest.Hwnd = ToHex(discovered);
            return discovered;
        }

        return IntPtr.Zero;
    }

    private string CaptureToNextFile(string capturesDirectory, string label, IntPtr hwnd)
    {
        Directory.CreateDirectory(capturesDirectory);
        var prefix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        var path = Path.Combine(capturesDirectory, $"{prefix}_{SanitizeLabel(label)}.png");
        _captureService.Capture(hwnd, path);
        return path;
    }

    private static bool TryParseHwnd(string value, out IntPtr hwnd)
    {
        var normalized = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? value[2..]
            : value;

        if (long.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed))
        {
            hwnd = (IntPtr)parsed;
            return true;
        }

        hwnd = IntPtr.Zero;
        return false;
    }

    private static string ToHex(IntPtr hwnd) => $"0x{hwnd.ToInt64():X}";

    private static string SanitizeLabel(string label)
    {
        var builder = new StringBuilder(label.Length);
        foreach (var character in label)
        {
            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-');
        }

        return builder.ToString().Trim('-');
    }

    private static IReadOnlyList<string> ReadTranscriptTail(string transcriptPath, int tailLines)
    {
        if (tailLines <= 0 || string.IsNullOrWhiteSpace(transcriptPath) || !File.Exists(transcriptPath))
        {
            return [];
        }

        try
        {
            using var stream = new FileStream(transcriptPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            var lines = new Queue<string>();

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine() ?? string.Empty;
                lines.Enqueue(line);
                while (lines.Count > tailLines)
                {
                    lines.Dequeue();
                }
            }

            return lines.ToArray();
        }
        catch (IOException)
        {
            return [];
        }
    }
}
