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
}
