[CmdletBinding()]
param(
    [ValidateSet("release", "local")]
    [string]$Source = "release",
    [string]$Repo = "11ArkaN/WtAgent",
    [string]$Version = "",
    [string]$AssetName = "wt-agent-win-x64.zip",
    [string]$Configuration = "Release",
    [string]$InstallRoot = "",
    [string]$BinDir = ""
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$projectPath = Join-Path $repoRoot "src\\WtAgent\\WtAgent.csproj"
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "wt-agent-install"

if ([string]::IsNullOrWhiteSpace($InstallRoot)) {
    $InstallRoot = Join-Path $env:LOCALAPPDATA "wt-agent\\current"
}

if ([string]::IsNullOrWhiteSpace($BinDir)) {
    $BinDir = Join-Path $HOME ".local\\bin"
}

$InstallRoot = [System.IO.Path]::GetFullPath($InstallRoot)
$BinDir = [System.IO.Path]::GetFullPath($BinDir)

if ($Source -eq "local" -and -not (Test-Path $projectPath)) {
    throw "Project file not found at '$projectPath'."
}

New-Item -ItemType Directory -Force $InstallRoot | Out-Null
New-Item -ItemType Directory -Force $BinDir | Out-Null
$launcherPath = Join-Path $BinDir "wt-agent.cmd"

function Ensure-CleanDirectory {
    param([string]$Path)

    if (Test-Path $Path) {
        Remove-Item $Path -Recurse -Force
    }

    New-Item -ItemType Directory -Force $Path | Out-Null
}

function Install-FromLocalBuild {
    dotnet publish $projectPath -c $Configuration -o $InstallRoot | Out-Null
}

function Get-GitHubReleaseAsset {
    param(
        [string]$Repository,
        [string]$RequestedVersion,
        [string]$RequestedAssetName,
        [string]$DestinationDirectory
    )

    Ensure-CleanDirectory -Path $DestinationDirectory

    $tagArgument = if ([string]::IsNullOrWhiteSpace($RequestedVersion)) { "" } else { $RequestedVersion }
    $downloadCommand = if ([string]::IsNullOrWhiteSpace($tagArgument)) {
        "gh release download --repo `"$Repository`" --pattern `"$RequestedAssetName`" --dir `"$DestinationDirectory`" --clobber"
    }
    else {
        "gh release download `"$tagArgument`" --repo `"$Repository`" --pattern `"$RequestedAssetName`" --dir `"$DestinationDirectory`" --clobber"
    }

    Invoke-Expression $downloadCommand | Out-Null

    $assetPath = Join-Path $DestinationDirectory $RequestedAssetName
    if (-not (Test-Path $assetPath)) {
        throw "Release asset '$RequestedAssetName' was not downloaded from '$Repository'."
    }

    return $assetPath
}

function Install-FromRelease {
    param(
        [string]$Repository,
        [string]$RequestedVersion,
        [string]$RequestedAssetName
    )

    $downloadDirectory = Join-Path $tempRoot "download"
    $extractDirectory = Join-Path $tempRoot "extract"
    $assetPath = Get-GitHubReleaseAsset -Repository $Repository -RequestedVersion $RequestedVersion -RequestedAssetName $RequestedAssetName -DestinationDirectory $downloadDirectory

    Ensure-CleanDirectory -Path $extractDirectory
    Expand-Archive -Path $assetPath -DestinationPath $extractDirectory -Force

    Get-ChildItem -Path $extractDirectory | ForEach-Object {
        $destination = Join-Path $InstallRoot $_.Name
        if (Test-Path $destination) {
            Remove-Item $destination -Recurse -Force
        }
        Copy-Item -Path $_.FullName -Destination $destination -Recurse -Force
    }

    if (-not (Test-Path (Join-Path $InstallRoot "WtAgent.exe"))) {
        throw "Installed release does not contain WtAgent.exe."
    }
}

switch ($Source) {
    "release" { Install-FromRelease -Repository $Repo -RequestedVersion $Version -RequestedAssetName $AssetName }
    "local" { Install-FromLocalBuild }
}
$launcherContent = @"
@echo off
"$InstallRoot\WtAgent.exe" %*
"@

Set-Content -Path $launcherPath -Value $launcherContent -Encoding ASCII

$userPath = [Environment]::GetEnvironmentVariable("Path", "User")
$pathEntries = @()
if (-not [string]::IsNullOrWhiteSpace($userPath)) {
    $pathEntries = $userPath.Split(';', [System.StringSplitOptions]::RemoveEmptyEntries)
}

$normalizedSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$dedupedEntries = New-Object System.Collections.Generic.List[string]
foreach ($entry in $pathEntries) {
    $normalized = $entry.TrimEnd('\')
    if ($normalizedSet.Add($normalized)) {
        [void]$dedupedEntries.Add($entry)
    }
}

$pathUpdated = $normalizedSet.Add($BinDir.TrimEnd('\'))
if ($pathUpdated) {
    [void]$dedupedEntries.Add($BinDir)
}

[Environment]::SetEnvironmentVariable("Path", ($dedupedEntries -join ';'), "User")

$result = [ordered]@{
    status = "ok"
    source = $Source
    repo = $Repo
    version = if ([string]::IsNullOrWhiteSpace($Version)) { "latest" } else { $Version }
    assetName = $AssetName
    installRoot = $InstallRoot
    launcherPath = $launcherPath
    pathUpdated = $pathUpdated
}

$result | ConvertTo-Json
