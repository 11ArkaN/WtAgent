namespace WtAgent;

internal static class RegistryProjector
{
    public static AgentRunResult ToResult(RegistryRun run)
    {
        return new AgentRunResult
        {
            RunId = run.RunId,
            Status = run.Status,
            ExitCode = null,
            Artifacts = run.Artifacts,
            Window = new WindowInfo
            {
                Title = run.WindowTitle,
                Profile = run.Profile,
                Pid = run.Pid,
                Hwnd = run.Hwnd
            },
            Timing = new TimingInfo
            {
                StartedAtUtc = run.StartedAtUtc,
                CompletedAtUtc = run.CompletedAtUtc,
                DurationMs = run.CompletedAtUtc is { } completed
                    ? (long)(completed - run.StartedAtUtc).TotalMilliseconds
                    : null
            },
            Capture = new CaptureInfo
            {
                ModeRequested = "auto",
                ModeUsed = File.Exists(run.Artifacts.PngPath) ? "fallback-printwindow" : "none",
                BlankFrameDetected = false
            }
        };
    }
}
