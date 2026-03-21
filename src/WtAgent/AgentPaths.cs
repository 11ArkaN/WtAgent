namespace WtAgent;

internal static class AgentPaths
{
    public static (string RootDirectory, string StateDirectory, string RunsDirectory) ResolveRoot(string? artifactsDirectory, string workingDirectory)
    {
        var root = artifactsDirectory is { Length: > 0 }
            ? Path.GetFullPath(artifactsDirectory)
            : Path.Combine(workingDirectory, ".wt-agent");

        var state = Path.Combine(root, "state");
        var runs = Path.Combine(root, "runs");

        Directory.CreateDirectory(root);
        Directory.CreateDirectory(state);
        Directory.CreateDirectory(runs);

        return (root, state, runs);
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
}
