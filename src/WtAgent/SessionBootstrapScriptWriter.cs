using System.Text;

namespace WtAgent;

internal static class SessionBootstrapScriptWriter
{
    public static void Write(SessionLayout layout, StartSessionArguments arguments, TerminalProfile profile, string title)
    {
        File.WriteAllText(layout.BootstrapScriptPath, BuildBootstrap(layout, arguments, profile, title), Encoding.UTF8);
    }

    private static string BuildBootstrap(SessionLayout layout, StartSessionArguments arguments, TerminalProfile profile, string title)
    {
        var template = """
$ErrorActionPreference = 'Stop'
$host.UI.RawUI.WindowTitle = '__TITLE__'
$transcriptPath = '__TRANSCRIPT__'
$readyPath = '__READY__'
$statePath = '__STATE__'
$cwd = '__CWD__'

New-Item -ItemType Directory -Force -Path (Split-Path $transcriptPath) | Out-Null
Set-Location -LiteralPath $cwd
Start-Transcript -Path $transcriptPath -Force | Out-Null

$script:WtAgentPromptSerial = 0

function Write-WtAgentPromptState {
    param([int] $ExitCode)

    $cwdValue = $executionContext.SessionState.Path.CurrentLocation.Path
    $promptValue = "PS $cwdValue> "
    [pscustomobject]@{
        promptSerial = $script:WtAgentPromptSerial
        lastExitCode = $ExitCode
        cwd = $cwdValue
        prompt = $promptValue
        updatedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    } | ConvertTo-Json -Depth 5 | Set-Content -Path $statePath -Encoding UTF8

    return $promptValue
}

function global:prompt {
    $script:WtAgentPromptSerial++

    $exitCode =
        if ($global:LASTEXITCODE -ne $null) { [int]$global:LASTEXITCODE }
        elseif ($?) { 0 }
        else { 1 }

    $promptValue = Write-WtAgentPromptState -ExitCode $exitCode

    if ($script:WtAgentPromptSerial -eq 1) {
        [pscustomobject]@{
            ready = $true
            updatedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
        } | ConvertTo-Json -Depth 4 | Set-Content -Path $readyPath -Encoding UTF8
    }

    return $promptValue
}

Write-Host 'WT Agent session'
Write-Host 'Profile: __PROFILENAME__'
Write-Host "CWD: $cwd"
Write-Host ''
""";

        return template
            .Replace("__TITLE__", Escape(title), StringComparison.Ordinal)
            .Replace("__TRANSCRIPT__", Escape(layout.TranscriptPath), StringComparison.Ordinal)
            .Replace("__READY__", Escape(layout.ReadyFilePath), StringComparison.Ordinal)
            .Replace("__STATE__", Escape(layout.PromptStateFilePath), StringComparison.Ordinal)
            .Replace("__CWD__", Escape(arguments.WorkingDirectory), StringComparison.Ordinal)
            .Replace("__PROFILENAME__", Escape(profile.Name), StringComparison.Ordinal);
    }

    private static string Escape(string input) => input.Replace("'", "''", StringComparison.Ordinal);
}
