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
$donePath = '__DONE__'
$commandPath = '__COMMANDPATH__'
$cwd = '__CWD__'
$mode = '__MODE__'
$profileShell = '__PROFILESHELL__'
$profileFlavor = '__PROFILEFLAVOR__'

New-Item -ItemType Directory -Force -Path (Split-Path $stdoutPath) | Out-Null
Set-Location -LiteralPath $cwd
Start-Transcript -Path $transcriptPath -Force | Out-Null

function New-PowerShellArgs([string]$scriptPath) {
    return "-NoLogo -NoProfile -ExecutionPolicy Bypass -Command & '$scriptPath'; if (`$LASTEXITCODE -ne `$null) { exit `$LASTEXITCODE } elseif (-not `$?) { exit 1 }"
}

function Invoke-ChildProcess {
    param(
        [string]$Mode,
        [string]$ProfileShell,
        [string]$ProfileFlavor
    )

    $commandText = [IO.File]::ReadAllText($commandPath)
    $tempRoot = Split-Path $commandPath

    switch ($Mode) {
        'powershell' {
            $scriptPath = Join-Path $tempRoot 'payload.ps1'
            [IO.File]::WriteAllText($scriptPath, $commandText, [Text.UTF8Encoding]::new($false))
            return Start-Process -FilePath "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -ArgumentList (New-PowerShellArgs $scriptPath) -Wait -PassThru -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
        }
        'raw' {
            switch ($ProfileFlavor) {
                'Cmd' {
                    $cmdPath = Join-Path $tempRoot 'payload.cmd'
                    [IO.File]::WriteAllText($cmdPath, $commandText, [Text.UTF8Encoding]::new($false))
                    return Start-Process -FilePath "$env:SystemRoot\System32\cmd.exe" -ArgumentList "/d /c `"$cmdPath`"" -Wait -PassThru -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
                }
                'WindowsPowerShell' {
                    $scriptPath = Join-Path $tempRoot 'payload.ps1'
                    [IO.File]::WriteAllText($scriptPath, $commandText, [Text.UTF8Encoding]::new($false))
                    return Start-Process -FilePath "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -ArgumentList (New-PowerShellArgs $scriptPath) -Wait -PassThru -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
                }
                'Pwsh' {
                    $scriptPath = Join-Path $tempRoot 'payload.ps1'
                    [IO.File]::WriteAllText($scriptPath, $commandText, [Text.UTF8Encoding]::new($false))
                    return Start-Process -FilePath $ProfileShell -ArgumentList (New-PowerShellArgs $scriptPath) -Wait -PassThru -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
                }
                'Bash' {
                    $scriptPath = Join-Path $tempRoot 'payload.sh'
                    [IO.File]::WriteAllText($scriptPath, $commandText, [Text.UTF8Encoding]::new($false))
                    return Start-Process -FilePath $ProfileShell -ArgumentList "-lc `"bash '$scriptPath'`"" -Wait -PassThru -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
                }
                'Wsl' {
                    return Start-Process -FilePath 'wsl.exe' -ArgumentList @('bash','-lc', $commandText) -Wait -PassThru -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
                }
                default {
                    throw "Raw mode is not supported for profile flavor '$ProfileFlavor'."
                }
            }
        }
        default {
            throw "Unsupported shell command mode '$Mode'."
        }
    }
}

try {
    Write-Host 'WT Agent run'
    Write-Host 'Profile: __PROFILENAME__'
    Write-Host "Mode: $mode"
    Write-Host "CWD: $cwd"
    Write-Host ''
    $proc = Invoke-ChildProcess -Mode $mode -ProfileShell $profileShell -ProfileFlavor $profileFlavor
    if (Test-Path $stdoutPath) {
        $stdout = Get-Content -Path $stdoutPath -Raw
        if ($stdout) { Write-Host $stdout -NoNewline }
    }
    if (Test-Path $stderrPath) {
        $stderr = Get-Content -Path $stderrPath -Raw
        if ($stderr) { Write-Host $stderr -ForegroundColor Red -NoNewline }
    }
    [pscustomobject]@{ exitCode = $proc.ExitCode } | ConvertTo-Json -Depth 4 | Set-Content -Path $donePath -Encoding UTF8
    $global:LASTEXITCODE = $proc.ExitCode
}
catch {
    $_ | Out-String | Set-Content -Path $stderrPath -Encoding UTF8
    [pscustomobject]@{ exitCode = 1; error = $_.Exception.Message } | ConvertTo-Json -Depth 4 | Set-Content -Path $donePath -Encoding UTF8
    Write-Host $_.Exception.Message -ForegroundColor Red
    $global:LASTEXITCODE = 1
}
finally {
    Stop-Transcript | Out-Null
}
""";

        return template
            .Replace("__TITLE__", Escape(title), StringComparison.Ordinal)
            .Replace("__STDOUT__", Escape(layout.Artifacts.StdoutPath), StringComparison.Ordinal)
            .Replace("__STDERR__", Escape(layout.Artifacts.StderrPath), StringComparison.Ordinal)
            .Replace("__TRANSCRIPT__", Escape(layout.Artifacts.TranscriptPath), StringComparison.Ordinal)
            .Replace("__DONE__", Escape(layout.DoneFilePath), StringComparison.Ordinal)
            .Replace("__COMMANDPATH__", Escape(layout.CommandFilePath), StringComparison.Ordinal)
            .Replace("__CWD__", Escape(arguments.WorkingDirectory), StringComparison.Ordinal)
            .Replace("__MODE__", arguments.ShellCommandMode == ShellCommandMode.Raw ? "raw" : "powershell", StringComparison.Ordinal)
            .Replace("__PROFILESHELL__", Escape(profile.Commandline ?? string.Empty), StringComparison.Ordinal)
            .Replace("__PROFILEFLAVOR__", profile.Flavor.ToString(), StringComparison.Ordinal)
            .Replace("__PROFILENAME__", Escape(profile.Name), StringComparison.Ordinal);
    }

    private static string Escape(string input) => input.Replace("'", "''", StringComparison.Ordinal);
}
