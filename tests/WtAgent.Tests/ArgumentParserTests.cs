using WtAgent;

namespace WtAgent.Tests;

public sealed class ArgumentParserTests
{
    [Fact]
    public void ParseRun_UsesDefaults_WhenOptionalValuesAreMissing()
    {
        var result = ArgumentParser.ParseRun(["--command", "Write-Output test", "--profile", "Windows PowerShell"]);

        Assert.True(result.Success);
        Assert.NotNull(result.RunArguments);
        Assert.Equal(ShellCommandMode.PowerShell, result.RunArguments!.ShellCommandMode);
        Assert.Equal(160, result.RunArguments.Columns);
        Assert.Equal(48, result.RunArguments.Rows);
    }

    [Fact]
    public void ParseRun_RejectsMissingCommand()
    {
        var result = ArgumentParser.ParseRun(["--profile", "Windows PowerShell"]);

        Assert.False(result.Success);
        Assert.Contains("command", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseSessionStart_UsesDefaults()
    {
        var result = ArgumentParser.ParseSessionStart(["--profile", "Windows PowerShell"]);

        Assert.True(result.Success);
        Assert.NotNull(result.Arguments);
        Assert.Equal(160, result.Arguments!.Columns);
        Assert.Equal(48, result.Arguments.Rows);
    }

    [Fact]
    public void ParseSessionSend_SupportsInteractiveMode()
    {
        var result = ArgumentParser.ParseSessionSend([
            "--session-id", "session-1",
            "--input", "wsl",
            "--wait-mode", "none",
            "--submit", "true",
            "--capture", "true"
        ]);

        Assert.True(result.Success);
        Assert.NotNull(result.Arguments);
        Assert.Equal(SessionWaitMode.None, result.Arguments!.WaitMode);
        Assert.True(result.Arguments.CaptureAfterSend);
        Assert.True(result.Arguments.Submit);
    }

    [Fact]
    public void ParseSessionSubmit_UsesPromptWaitByDefault()
    {
        var result = ArgumentParser.ParseSessionSubmit(["--session-id", "session-1"]);

        Assert.True(result.Success);
        Assert.NotNull(result.Arguments);
        Assert.Equal(SessionWaitMode.Prompt, result.Arguments!.WaitMode);
    }
}
