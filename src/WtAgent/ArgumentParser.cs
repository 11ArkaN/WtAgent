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
}
