using System.Diagnostics;
using System.Text.Json;

namespace WtAgent;

internal sealed class WindowsTerminalRunner
{
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly RegistryStore _registry;
    private readonly WindowCaptureService _captureService = new();

    public WindowsTerminalRunner(JsonSerializerOptions jsonOptions, RegistryStore registry)
    {
        _jsonOptions = jsonOptions;
        _registry = registry;
    }

    public async Task<AgentRunResult> RunAsync(
        RunArguments arguments,
        RunLayout layout,
        TerminalProfile profile,
        string title,
        DateTimeOffset startedAtUtc)
    {
        var registryRun = new RegistryRun
        {
            RunId = layout.RunId,
            Status = "launching",
            Profile = profile.Name,
            WindowTitle = title,
            Artifacts = layout.Artifacts,
            StartedAtUtc = startedAtUtc
        };

        await _registry.UpsertAsync(registryRun);

        LaunchResult? launch = null;
        WindowDiscoveryResult? window = null;
        BootstrapResult? bootstrap = null;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            BootstrapScriptWriter.Write(layout, arguments, profile, title);

            launch = WindowsTerminalLauncher.Launch(arguments, layout, profile, title);
            registryRun.Pid = launch.ProcessId;
            await _registry.UpsertAsync(registryRun);

            window = await WindowLocator.WaitForWindowAsync(title, launch.DesktopName, launch.ProcessId, TimeSpan.FromSeconds(20));
            registryRun.Hwnd = $"0x{window.Hwnd.ToInt64():X}";
            registryRun.Pid = window.ProcessId;
            registryRun.Status = "running";
            await _registry.UpsertAsync(registryRun);

            await BootstrapWatcher.WaitForReadyAsync(layout.ReadyFilePath, TimeSpan.FromSeconds(15));
            NativeMethods.PasteCommandAndSubmit(window.Hwnd, arguments.Command);
            bootstrap = await BootstrapWatcher.WaitForCompletionAsync(layout.DoneFilePath, TimeSpan.FromSeconds(arguments.TimeoutSeconds));
            await Task.Delay(500);

            var capture = _captureService.Capture(window.Hwnd, layout.Artifacts.PngPath);
            var status = bootstrap.ExitCode == 0 ? "ok" : "command_error";
            var result = CreateResult(layout, profile.Name, title, window, startedAtUtc, stopwatch.ElapsedMilliseconds, bootstrap.ExitCode, status, capture, null);
            await PersistResultAsync(result, registryRun, status);
            return result;
        }
        catch (TimeoutException ex)
        {
            var capture = window is null
                ? new CaptureResult("none", false)
                : _captureService.Capture(window.Hwnd, layout.Artifacts.PngPath);
            var result = CreateResult(layout, profile.Name, title, window, startedAtUtc, stopwatch.ElapsedMilliseconds, null, "timeout", capture, ex.Message);
            await PersistResultAsync(result, registryRun, "timeout");
            return result;
        }
        catch (Exception ex)
        {
            var capture = window is null
                ? new CaptureResult("none", false)
                : _captureService.Capture(window.Hwnd, layout.Artifacts.PngPath);
            var status = window is null ? "launch_error" : "capture_error";
            var result = CreateResult(layout, profile.Name, title, window, startedAtUtc, stopwatch.ElapsedMilliseconds, bootstrap?.ExitCode, status, capture, ex.Message);
            await PersistResultAsync(result, registryRun, status);
            return result;
        }
        finally
        {
            stopwatch.Stop();
            if (window is not null)
            {
                NativeMethods.PostMessage(window.Hwnd, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }

            if (launch is not null)
            {
                if (launch.ProcessHandle != IntPtr.Zero)
                {
                    NativeMethods.CloseHandle(launch.ProcessHandle);
                }

                if (launch.ThreadHandle != IntPtr.Zero)
                {
                    NativeMethods.CloseHandle(launch.ThreadHandle);
                }

                if (!string.IsNullOrWhiteSpace(launch.DesktopName))
                {
                    NativeMethods.CloseDesktopByName(launch.DesktopName);
                }
            }
        }
    }

    private async Task PersistResultAsync(AgentRunResult result, RegistryRun registryRun, string status)
    {
        registryRun.Status = status;
        registryRun.CompletedAtUtc = result.Timing.CompletedAtUtc;

        await File.WriteAllTextAsync(result.Artifacts.ResultPath, JsonSerializer.Serialize(result, _jsonOptions));
        await File.WriteAllTextAsync(result.Artifacts.MetaPath, JsonSerializer.Serialize(new
        {
            runId = result.RunId,
            status = result.Status,
            result.Window,
            result.Capture,
            result.Timing
        }, _jsonOptions));

        await _registry.UpsertAsync(registryRun);
    }

    private static AgentRunResult CreateResult(
        RunLayout layout,
        string profileName,
        string title,
        WindowDiscoveryResult? window,
        DateTimeOffset startedAtUtc,
        long durationMs,
        int? exitCode,
        string status,
        CaptureResult capture,
        string? error)
    {
        return new AgentRunResult
        {
            RunId = layout.RunId,
            Status = status,
            ExitCode = exitCode,
            Error = error,
            Artifacts = layout.Artifacts,
            Window = new WindowInfo
            {
                Title = title,
                Profile = profileName,
                Pid = window?.ProcessId,
                Hwnd = window is null ? null : $"0x{window.Hwnd.ToInt64():X}"
            },
            Timing = new TimingInfo
            {
                StartedAtUtc = startedAtUtc,
                CompletedAtUtc = DateTimeOffset.UtcNow,
                DurationMs = durationMs
            },
            Capture = new CaptureInfo
            {
                ModeRequested = "auto",
                ModeUsed = capture.ModeUsed,
                BlankFrameDetected = capture.BlankFrameDetected
            }
        };
    }
}
