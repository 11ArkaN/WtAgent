namespace WtAgent;

internal static class AgentPaths
{
    public static (string RootDirectory, string StateDirectory, string RunsDirectory, string SessionsDirectory) ResolveRoot(string? artifactsDirectory, string workingDirectory)
    {
        var root = artifactsDirectory is { Length: > 0 }
            ? Path.GetFullPath(artifactsDirectory)
            : Path.Combine(workingDirectory, ".wt-agent");

        var state = Path.Combine(root, "state");
        var runs = Path.Combine(root, "runs");
        var sessions = Path.Combine(root, "sessions");

        Directory.CreateDirectory(root);
        Directory.CreateDirectory(state);
        Directory.CreateDirectory(runs);
        Directory.CreateDirectory(sessions);

        return (root, state, runs, sessions);
    }

    public static RunLayout CreateRunLayout(string? artifactsDirectory, string workingDirectory)
    {
        var root = ResolveRoot(artifactsDirectory, workingDirectory);
        var runId = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff") + "_" + Guid.NewGuid().ToString("N")[..8];
        var runDirectory = Path.Combine(root.RunsDirectory, runId);
        Directory.CreateDirectory(runDirectory);

        var resultPath = Path.Combine(runDirectory, "result.json");
        var metaPath = Path.Combine(runDirectory, "meta.json");

        return new RunLayout
        {
            RootDirectory = root.RootDirectory,
            StateDirectory = root.StateDirectory,
            RunsDirectory = root.RunsDirectory,
            RunDirectory = runDirectory,
            RunId = runId,
            BootstrapScriptPath = Path.Combine(runDirectory, "bootstrap.ps1"),
            CommandFilePath = Path.Combine(runDirectory, "payload.txt"),
            ReadyFilePath = Path.Combine(runDirectory, "ready.json"),
            DoneFilePath = Path.Combine(runDirectory, "done.json"),
            ResultFilePath = resultPath,
            MetadataFilePath = metaPath,
            Artifacts = new ArtifactSet
            {
                PngPath = Path.Combine(runDirectory, "terminal.png"),
                StdoutPath = Path.Combine(runDirectory, "stdout.txt"),
                StderrPath = Path.Combine(runDirectory, "stderr.txt"),
                TranscriptPath = Path.Combine(runDirectory, "transcript.txt"),
                ResultPath = resultPath,
                MetaPath = metaPath
            }
        };
    }

    public static SessionLayout CreateSessionLayout(string? artifactsDirectory, string workingDirectory)
    {
        var root = ResolveRoot(artifactsDirectory, workingDirectory);
        var sessionId = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff") + "_" + Guid.NewGuid().ToString("N")[..8];
        var sessionDirectory = Path.Combine(root.SessionsDirectory, sessionId);
        var capturesDirectory = Path.Combine(sessionDirectory, "captures");

        Directory.CreateDirectory(sessionDirectory);
        Directory.CreateDirectory(capturesDirectory);

        return new SessionLayout
        {
            RootDirectory = root.RootDirectory,
            StateDirectory = root.StateDirectory,
            SessionsDirectory = root.SessionsDirectory,
            SessionDirectory = sessionDirectory,
            SessionId = sessionId,
            BootstrapScriptPath = Path.Combine(sessionDirectory, "bootstrap.ps1"),
            ReadyFilePath = Path.Combine(sessionDirectory, "ready.json"),
            PromptStateFilePath = Path.Combine(sessionDirectory, "prompt-state.json"),
            ManifestPath = Path.Combine(sessionDirectory, "session.json"),
            CapturesDirectory = capturesDirectory,
            TranscriptPath = Path.Combine(sessionDirectory, "transcript.txt")
        };
    }

    public static string ResolveSessionManifestPath(string? artifactsDirectory, string workingDirectory, string sessionId)
    {
        var root = ResolveRoot(artifactsDirectory, workingDirectory);
        return Path.Combine(root.SessionsDirectory, sessionId, "session.json");
    }
}
