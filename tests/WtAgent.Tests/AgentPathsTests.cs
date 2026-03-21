using WtAgent;

namespace WtAgent.Tests;

public sealed class AgentPathsTests
{
    [Fact]
    public void ResolveRoot_DefaultsToWorkspaceDirectory()
    {
        var workspace = Path.Combine(Path.GetTempPath(), "wt-agent-tests", Guid.NewGuid().ToString("N"));

        var root = AgentPaths.ResolveRoot(null, workspace);

        Assert.Equal(Path.Combine(workspace, ".wt-agent"), root.RootDirectory);
        Assert.Equal(Path.Combine(root.RootDirectory, "runs"), root.RunsDirectory);
        Assert.Equal(Path.Combine(root.RootDirectory, "sessions"), root.SessionsDirectory);
    }
}
