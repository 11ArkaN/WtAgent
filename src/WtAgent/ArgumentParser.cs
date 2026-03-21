namespace WtAgent;

internal static class ArgumentParser
{
    public static (bool Success, string? ErrorMessage, RunArguments? RunArguments) ParseRun(string[] args)
    {
        var map = ParseMap(args);
        if (!map.TryGetValue("command", out var command) || string.IsNullOrWhiteSpace(command))
        {
            return (false, "Missing required --command value.", null);
        }

        if (!map.TryGetValue("profile", out var profile) || string.IsNullOrWhiteSpace(profile))
        {
            return (false, "Missing required --profile value.", null);
        }

        var cwd = map.TryGetValue("cwd", out var workingDirectory) && !string.IsNullOrWhiteSpace(workingDirectory)
            ? Path.GetFullPath(workingDirectory)
            : Environment.CurrentDirectory;

        if (!Directory.Exists(cwd))
        {
            return (false, $"Working directory '{cwd}' does not exist.", null);
        }

        var mode = map.TryGetValue("shell-command-mode", out var modeValue) && modeValue.Equals("raw", StringComparison.OrdinalIgnoreCase)
            ? ShellCommandMode.Raw
            : ShellCommandMode.PowerShell;

        var timeoutSeconds = ParseInt(map, "timeout-sec", 120);
        var columns = ParseInt(map, "cols", 160);
        var rows = ParseInt(map, "rows", 48);
        var artifactsDir = map.TryGetValue("artifacts-dir", out var artifactsValue) ? artifactsValue : null;

        return (true, null, new RunArguments(command, profile, cwd, mode, timeoutSeconds, columns, rows, artifactsDir));
    }

    public static (bool Success, string? ErrorMessage, InspectArguments? InspectArguments) ParseInspect(string[] args)
    {
        var map = ParseMap(args);
        if (!map.TryGetValue("run-id", out var runId) || string.IsNullOrWhiteSpace(runId))
        {
            return (false, "Missing required --run-id value.", null);
        }

        var artifactsDir = map.TryGetValue("artifacts-dir", out var artifactsValue) ? artifactsValue : null;
        return (true, null, new InspectArguments(runId, artifactsDir));
    }

    public static (bool Success, string? ErrorMessage, CleanupArguments? CleanupArguments) ParseCleanup(string[] args)
    {
        var map = ParseMap(args);
        var hours = ParseInt(map, "older-than-hours", 24);
        var artifactsDir = map.TryGetValue("artifacts-dir", out var artifactsValue) ? artifactsValue : null;
        return (true, null, new CleanupArguments(hours, artifactsDir));
    }

    public static (bool Success, string? ErrorMessage, StartSessionArguments? Arguments) ParseSessionStart(string[] args)
    {
        var map = ParseMap(args);
        if (!map.TryGetValue("profile", out var profile) || string.IsNullOrWhiteSpace(profile))
        {
            return (false, "Missing required --profile value.", null);
        }

        var cwd = map.TryGetValue("cwd", out var workingDirectory) && !string.IsNullOrWhiteSpace(workingDirectory)
            ? Path.GetFullPath(workingDirectory)
            : Environment.CurrentDirectory;

        if (!Directory.Exists(cwd))
        {
            return (false, $"Working directory '{cwd}' does not exist.", null);
        }

        var columns = ParseInt(map, "cols", 160);
        var rows = ParseInt(map, "rows", 48);
        var artifactsDir = map.TryGetValue("artifacts-dir", out var artifactsValue) ? artifactsValue : null;
        return (true, null, new StartSessionArguments(profile, cwd, columns, rows, artifactsDir));
    }

    public static (bool Success, string? ErrorMessage, SessionSendArguments? Arguments) ParseSessionSend(string[] args)
    {
        var map = ParseMap(args);
        if (!map.TryGetValue("session-id", out var sessionId) || string.IsNullOrWhiteSpace(sessionId))
        {
            return (false, "Missing required --session-id value.", null);
        }

        if (!map.TryGetValue("input", out var input))
        {
            return (false, "Missing required --input value.", null);
        }

        var waitMode = map.TryGetValue("wait-mode", out var waitModeValue) && waitModeValue.Equals("none", StringComparison.OrdinalIgnoreCase)
            ? SessionWaitMode.None
            : SessionWaitMode.Prompt;
        var timeout = ParseInt(map, "timeout-sec", 60);
        var postWaitMs = ParseInt(map, "post-wait-ms", 700);
        var captureAfterSend = ParseBool(map, "capture", true);
        var submit = ParseBool(map, "submit", true);
        var artifactsDir = map.TryGetValue("artifacts-dir", out var artifactsValue) ? artifactsValue : null;

        return (true, null, new SessionSendArguments(sessionId, input, waitMode, timeout, postWaitMs, captureAfterSend, submit, artifactsDir));
    }

    public static (bool Success, string? ErrorMessage, SessionStatusArguments? Arguments) ParseSessionStatus(string[] args)
    {
        var map = ParseMap(args);
        if (!map.TryGetValue("session-id", out var sessionId) || string.IsNullOrWhiteSpace(sessionId))
        {
            return (false, "Missing required --session-id value.", null);
        }

        var tailLines = ParseInt(map, "tail-lines", 40);
        var artifactsDir = map.TryGetValue("artifacts-dir", out var artifactsValue) ? artifactsValue : null;
        return (true, null, new SessionStatusArguments(sessionId, tailLines, artifactsDir));
    }

    public static (bool Success, string? ErrorMessage, SessionInspectArguments? Arguments) ParseSessionInspect(string[] args)
    {
        var map = ParseMap(args);
        if (!map.TryGetValue("session-id", out var sessionId) || string.IsNullOrWhiteSpace(sessionId))
        {
            return (false, "Missing required --session-id value.", null);
        }

        var tailLines = ParseInt(map, "tail-lines", 80);
        var artifactsDir = map.TryGetValue("artifacts-dir", out var artifactsValue) ? artifactsValue : null;
        return (true, null, new SessionInspectArguments(sessionId, tailLines, artifactsDir));
    }

    public static (bool Success, string? ErrorMessage, SessionListArguments? Arguments) ParseSessionList(string[] args)
    {
        var map = ParseMap(args);
        var activeOnly = ParseBool(map, "active-only", false);
        var artifactsDir = map.TryGetValue("artifacts-dir", out var artifactsValue) ? artifactsValue : null;
        return (true, null, new SessionListArguments(activeOnly, artifactsDir));
    }

    public static (bool Success, string? ErrorMessage, SessionCaptureArguments? Arguments) ParseSessionCapture(string[] args)
    {
        var map = ParseMap(args);
        if (!map.TryGetValue("session-id", out var sessionId) || string.IsNullOrWhiteSpace(sessionId))
        {
            return (false, "Missing required --session-id value.", null);
        }

        var label = map.TryGetValue("label", out var labelValue) ? labelValue : null;
        var postWaitMs = ParseInt(map, "post-wait-ms", 400);
        var artifactsDir = map.TryGetValue("artifacts-dir", out var artifactsValue) ? artifactsValue : null;
        return (true, null, new SessionCaptureArguments(sessionId, label, postWaitMs, artifactsDir));
    }

    public static (bool Success, string? ErrorMessage, SessionInterruptArguments? Arguments) ParseSessionInterrupt(string[] args)
    {
        var map = ParseMap(args);
        if (!map.TryGetValue("session-id", out var sessionId) || string.IsNullOrWhiteSpace(sessionId))
        {
            return (false, "Missing required --session-id value.", null);
        }

        var postWaitMs = ParseInt(map, "post-wait-ms", 600);
        var captureAfterInterrupt = ParseBool(map, "capture", true);
        var artifactsDir = map.TryGetValue("artifacts-dir", out var artifactsValue) ? artifactsValue : null;
        return (true, null, new SessionInterruptArguments(sessionId, postWaitMs, captureAfterInterrupt, artifactsDir));
    }

    public static (bool Success, string? ErrorMessage, SessionSubmitArguments? Arguments) ParseSessionSubmit(string[] args)
    {
        var map = ParseMap(args);
        if (!map.TryGetValue("session-id", out var sessionId) || string.IsNullOrWhiteSpace(sessionId))
        {
            return (false, "Missing required --session-id value.", null);
        }

        var waitMode = map.TryGetValue("wait-mode", out var waitModeValue) && waitModeValue.Equals("none", StringComparison.OrdinalIgnoreCase)
            ? SessionWaitMode.None
            : SessionWaitMode.Prompt;
        var timeout = ParseInt(map, "timeout-sec", 60);
        var postWaitMs = ParseInt(map, "post-wait-ms", 700);
        var captureAfterSubmit = ParseBool(map, "capture", true);
        var artifactsDir = map.TryGetValue("artifacts-dir", out var artifactsValue) ? artifactsValue : null;

        return (true, null, new SessionSubmitArguments(sessionId, waitMode, timeout, postWaitMs, captureAfterSubmit, artifactsDir));
    }

    public static (bool Success, string? ErrorMessage, SessionStopArguments? Arguments) ParseSessionStop(string[] args)
    {
        var map = ParseMap(args);
        if (!map.TryGetValue("session-id", out var sessionId) || string.IsNullOrWhiteSpace(sessionId))
        {
            return (false, "Missing required --session-id value.", null);
        }

        var interrupt = ParseBool(map, "interrupt", false);
        var postWaitMs = ParseInt(map, "post-wait-ms", 300);
        var artifactsDir = map.TryGetValue("artifacts-dir", out var artifactsValue) ? artifactsValue : null;
        return (true, null, new SessionStopArguments(sessionId, interrupt, postWaitMs, artifactsDir));
    }

    public static (bool Success, string? ErrorMessage, SessionEnterWslArguments? Arguments) ParseSessionEnterWsl(string[] args)
    {
        var map = ParseMap(args);
        if (!map.TryGetValue("session-id", out var sessionId) || string.IsNullOrWhiteSpace(sessionId))
        {
            return (false, "Missing required --session-id value.", null);
        }

        var distribution = map.TryGetValue("distribution", out var distributionValue) ? distributionValue : null;
        var timeout = ParseInt(map, "timeout-sec", 20);
        var postWaitMs = ParseInt(map, "post-wait-ms", 1200);
        var captureAfterEnter = ParseBool(map, "capture", true);
        var artifactsDir = map.TryGetValue("artifacts-dir", out var artifactsValue) ? artifactsValue : null;
        return (true, null, new SessionEnterWslArguments(sessionId, distribution, timeout, postWaitMs, captureAfterEnter, artifactsDir));
    }

    private static Dictionary<string, string> ParseMap(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = token[2..];
            var value = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[++i]
                : "true";
            map[key] = value;
        }

        return map;
    }

    private static int ParseInt(Dictionary<string, string> map, string key, int fallback)
    {
        return map.TryGetValue(key, out var raw) && int.TryParse(raw, out var parsed)
            ? parsed
            : fallback;
    }

    private static bool ParseBool(Dictionary<string, string> map, string key, bool fallback)
    {
        if (!map.TryGetValue(key, out var raw))
        {
            return fallback;
        }

        return raw.Equals("true", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("1", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }
}
