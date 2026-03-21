using System.Text;

namespace WtAgent;

internal static class BootstrapScriptWriter
{
    public static void Write(RunLayout layout, RunArguments arguments, TerminalProfile profile, string title)
    {
        File.WriteAllText(layout.CommandFilePath, arguments.Command, Encoding.UTF8);
        File.WriteAllText(layout.BootstrapScriptPath, BuildBootstrap(layout, arguments, profile, title), Encoding.UTF8);
    }

    private static string BuildBootstrap(RunLayout layout, RunArguments arguments, TerminalProfile profile, string title)
    {
        var template = """
$ErrorActionPreference = 'Stop'
$host.UI.RawUI.WindowTitle = '__TITLE__'
$stdoutPath = '__STDOUT__'
$stderrPath = '__STDERR__'
$transcriptPath = '__TRANSCRIPT__'
$readyPath = '__READY__'
$donePath = '__DONE__'
$cwd = '__CWD__'

New-Item -ItemType Directory -Force -Path (Split-Path $stdoutPath) | Out-Null
Set-Location -LiteralPath $cwd
Start-Transcript -Path $transcriptPath -Force | Out-Null

$script:WtAgentPromptCount = 0
$script:WtAgentDone = $false

function global:prompt {
    $script:WtAgentPromptCount++

    if ($script:WtAgentPromptCount -eq 1) {
        [pscustomobject]@{ ready = $true } | ConvertTo-Json -Depth 4 | Set-Content -Path $readyPath -Encoding UTF8
    }
    elseif (-not $script:WtAgentDone) {
        $exitCode =
            if ($global:LASTEXITCODE -ne $null) { [int]$global:LASTEXITCODE }
            elseif ($?) { 0 }
            else { 1 }

        [pscustomobject]@{ exitCode = $exitCode } | ConvertTo-Json -Depth 4 | Set-Content -Path $donePath -Encoding UTF8
        $script:WtAgentDone = $true
    }

    return "PS $($executionContext.SessionState.Path.CurrentLocation)> "
}

Write-Host 'WT Agent run'
Write-Host 'Profile: __PROFILENAME__'
Write-Host 'Mode: __MODE__'
Write-Host "CWD: $cwd"
Write-Host ''
""";

        return template
            .Replace("__TITLE__", Escape(title), StringComparison.Ordinal)
            .Replace("__STDOUT__", Escape(layout.Artifacts.StdoutPath), StringComparison.Ordinal)
            .Replace("__STDERR__", Escape(layout.Artifacts.StderrPath), StringComparison.Ordinal)
            .Replace("__TRANSCRIPT__", Escape(layout.Artifacts.TranscriptPath), StringComparison.Ordinal)
            .Replace("__READY__", Escape(layout.ReadyFilePath), StringComparison.Ordinal)
            .Replace("__DONE__", Escape(layout.DoneFilePath), StringComparison.Ordinal)
            .Replace("__CWD__", Escape(arguments.WorkingDirectory), StringComparison.Ordinal)
            .Replace("__MODE__", arguments.ShellCommandMode == ShellCommandMode.Raw ? "raw" : "powershell", StringComparison.Ordinal)
            .Replace("__PROFILENAME__", Escape(profile.Name), StringComparison.Ordinal);
    }

    private static string Escape(string input) => input.Replace("'", "''", StringComparison.Ordinal);
}
