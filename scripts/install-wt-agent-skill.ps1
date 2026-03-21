[CmdletBinding()]
param(
    [ValidateSet("release", "local")]
    [string]$Source = "release",
    [string]$Repo = "11ArkaN/WtAgent",
    [string]$Version = "",
    [string]$AssetName = "wt-agent-terminal-skill.zip",
    [string]$SkillName = "wt-agent-terminal",
    [string]$InstallRoot = ""
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$localSkillPath = Join-Path $repoRoot "skills\$SkillName"
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "wt-agent-skill-install"

if ([string]::IsNullOrWhiteSpace($InstallRoot)) {
    $codexHome = if ([string]::IsNullOrWhiteSpace($env:CODEX_HOME)) {
        Join-Path $HOME ".codex"
    }
    else {
        $env:CODEX_HOME
    }

    $InstallRoot = Join-Path $codexHome "skills"
}

$InstallRoot = [System.IO.Path]::GetFullPath($InstallRoot)
$destinationPath = Join-Path $InstallRoot $SkillName

function Ensure-CleanDirectory {
    param([string]$Path)

    if (Test-Path $Path) {
        Remove-Item $Path -Recurse -Force
    }

    New-Item -ItemType Directory -Force $Path | Out-Null
}

function Install-LocalSkill {
    if (-not (Test-Path $localSkillPath)) {
        throw "Local skill was not found at '$localSkillPath'."
    }

    if (Test-Path $destinationPath) {
        Remove-Item $destinationPath -Recurse -Force
    }

    New-Item -ItemType Directory -Force $InstallRoot | Out-Null
    Copy-Item -Path $localSkillPath -Destination $destinationPath -Recurse -Force
}

function Download-ReleaseAsset {
    param(
        [string]$Repository,
        [string]$RequestedVersion,
        [string]$RequestedAssetName,
        [string]$DestinationDirectory
    )

    Ensure-CleanDirectory -Path $DestinationDirectory

    $downloadCommand = if ([string]::IsNullOrWhiteSpace($RequestedVersion)) {
        "gh release download --repo `"$Repository`" --pattern `"$RequestedAssetName`" --dir `"$DestinationDirectory`" --clobber"
    }
    else {
        "gh release download `"$RequestedVersion`" --repo `"$Repository`" --pattern `"$RequestedAssetName`" --dir `"$DestinationDirectory`" --clobber"
    }

    Invoke-Expression $downloadCommand | Out-Null

    $assetPath = Join-Path $DestinationDirectory $RequestedAssetName
    if (-not (Test-Path $assetPath)) {
        throw "Skill release asset '$RequestedAssetName' was not downloaded from '$Repository'."
    }

    return $assetPath
}

function Install-ReleaseSkill {
    $downloadDirectory = Join-Path $tempRoot "download"
    $extractDirectory = Join-Path $tempRoot "extract"
    $assetPath = Download-ReleaseAsset -Repository $Repo -RequestedVersion $Version -RequestedAssetName $AssetName -DestinationDirectory $downloadDirectory

    Ensure-CleanDirectory -Path $extractDirectory
    Expand-Archive -Path $assetPath -DestinationPath $extractDirectory -Force

    $extractedRoot = Join-Path $extractDirectory $SkillName
    if (-not (Test-Path $extractedRoot)) {
        throw "Extracted release asset does not contain '$SkillName'."
    }

    if (Test-Path $destinationPath) {
        Remove-Item $destinationPath -Recurse -Force
    }

    New-Item -ItemType Directory -Force $InstallRoot | Out-Null
    Copy-Item -Path $extractedRoot -Destination $destinationPath -Recurse -Force
}

switch ($Source) {
    "release" { Install-ReleaseSkill }
    "local" { Install-LocalSkill }
}

$result = [ordered]@{
    status = "ok"
    source = $Source
    repo = $Repo
    version = if ([string]::IsNullOrWhiteSpace($Version)) { "latest" } else { $Version }
    assetName = $AssetName
    skillName = $SkillName
    installRoot = $InstallRoot
    destinationPath = $destinationPath
}

$result | ConvertTo-Json
