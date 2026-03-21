[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$InstallRoot = "",
    [switch]$InstallSkill,
    [ValidateSet("codex", "claude-code", "cursor", "gemini-cli", "opencode", "custom")]
    [string]$SkillAgent = "codex",
    [string]$SkillInstallRoot = ""
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$projectPath = Join-Path $repoRoot "src\\WtAgent\\WtAgent.csproj"

if ([string]::IsNullOrWhiteSpace($InstallRoot)) {
    $InstallRoot = Join-Path $repoRoot ".artifacts\\local-install"
}

$InstallRoot = [System.IO.Path]::GetFullPath($InstallRoot)

if (-not (Test-Path $projectPath)) {
    throw "Project file not found at '$projectPath'."
}

if (Test-Path $InstallRoot) {
    Remove-Item $InstallRoot -Recurse -Force
}

New-Item -ItemType Directory -Force $InstallRoot | Out-Null

dotnet publish $projectPath `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:DebugType=None `
    /p:DebugSymbols=false `
    -o $InstallRoot | Out-Null

$skillInstallResult = $null
if ($InstallSkill) {
    $skillScriptPath = Join-Path $scriptRoot "install-wt-agent-skill.ps1"
    if (-not (Test-Path $skillScriptPath)) {
        throw "Skill installer not found at '$skillScriptPath'."
    }

    $skillArgs = @{
        Source = "local"
        Agent = $SkillAgent
    }

    if (-not [string]::IsNullOrWhiteSpace($SkillInstallRoot)) {
        $skillArgs.InstallRoot = $SkillInstallRoot
    }

    $skillInstallResult = & $skillScriptPath @skillArgs | ConvertFrom-Json
}

$result = [ordered]@{
    status = "ok"
    installRoot = $InstallRoot
    executablePath = (Join-Path $InstallRoot "WtAgent.exe")
    skillInstalled = $InstallSkill.IsPresent
    skill = $skillInstallResult
}

$result | ConvertTo-Json -Depth 6
