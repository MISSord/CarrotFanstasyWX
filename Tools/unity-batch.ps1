#Requires -Version 5.1
<#
.SYNOPSIS
  Unity batchmode entry: code hot-update, AB build, PC player build.

.EXAMPLE
  .\Tools\unity-batch.ps1 -Command codeHotUpdate -Target StandaloneWindows64
  .\Tools\unity-batch.ps1 -Command abBuild -Upload
  .\Tools\unity-batch.ps1 -Command pcBuild -Channel dev
  .\Tools\unity-batch.ps1 -Command pcBuild -Channel prod

.NOTES
  Unity path: -UnityPath or env UNITY_EDITOR.
  Exit code matches Unity process (0 = success, 2 = recompile retry for pcBuild).
  See docs/BuildAndHotUpdateSOP.md
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("codeHotUpdate", "abBuild", "pcBuild")]
    [string] $Command,

    [string] $Target = "StandaloneWindows64",

    [switch] $Upload,

    [ValidateSet("dev", "staging", "prod")]
    [string] $RuntimeEnv,

    [ValidateSet("dev", "prod")]
    [string] $Channel,

    [switch] $ForceRebuild,

    [switch] $CopyStreaming,

    [switch] $SkipGenerate,

    [string] $UnityPath,

    [string] $LogFile
)

$ErrorActionPreference = "Stop"

if ($Command -eq "pcBuild" -and [string]::IsNullOrWhiteSpace($Channel)) {
    Write-Error "pcBuild requires -Channel dev|prod"
    exit 1
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "CarrotFantasy"
if (-not (Test-Path -LiteralPath $projectPath)) {
    Write-Error "Unity project not found: $projectPath"
    exit 1
}

if ([string]::IsNullOrWhiteSpace($UnityPath)) {
    $UnityPath = $env:UNITY_EDITOR
}
if ([string]::IsNullOrWhiteSpace($UnityPath)) {
    Write-Error "Set -UnityPath or env UNITY_EDITOR to Unity.exe."
    exit 1
}
if (-not (Test-Path -LiteralPath $UnityPath)) {
    Write-Error "Unity executable not found: $UnityPath"
    exit 1
}

$logsDir = Join-Path $repoRoot "Logs"
if (-not (Test-Path -LiteralPath $logsDir)) {
    New-Item -ItemType Directory -Path $logsDir | Out-Null
}

function Invoke-UnityBatch {
    param([string] $LogPath)

    $cfArgs = @(
        "-cfCommand=$Command",
        "-cfTarget=$Target",
        "-cfUpload=$($Upload.IsPresent.ToString().ToLowerInvariant())",
        "-cfForceRebuild=$($ForceRebuild.IsPresent.ToString().ToLowerInvariant())",
        "-cfCopyStreaming=$($CopyStreaming.IsPresent.ToString().ToLowerInvariant())",
        "-cfSkipGenerate=$($SkipGenerate.IsPresent.ToString().ToLowerInvariant())"
    )
    if (-not [string]::IsNullOrWhiteSpace($RuntimeEnv)) {
        $cfArgs += "-cfEnv=$RuntimeEnv"
    }
    if (-not [string]::IsNullOrWhiteSpace($Channel)) {
        $cfArgs += "-cfChannel=$Channel"
    }

    $unityArgs = @(
        "-batchmode",
        "-nographics",
        "-quit",
        "-projectPath", $projectPath,
        "-logFile", $LogPath,
        "-executeMethod", "CarrotFantasy.Editor.Batch.BuildCli.Run"
    ) + $cfArgs

    Write-Host "Unity: $UnityPath"
    Write-Host "Project: $projectPath"
    Write-Host "Log: $LogPath"
    Write-Host "Args: $($unityArgs -join ' ')"

    $proc = Start-Process -FilePath $UnityPath -ArgumentList $unityArgs -Wait -PassThru -NoNewWindow
    $code = $proc.ExitCode
    if ($null -eq $code) {
        $code = 1
    }
    return $code
}

if ([string]::IsNullOrWhiteSpace($LogFile)) {
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $LogFile = Join-Path $logsDir "unity-batch-$Command-$stamp.log"
}

$code = Invoke-UnityBatch -LogPath $LogFile

# pcBuild exit 2: scripting define just changed; re-run once after recompile.
if ($Command -eq "pcBuild" -and $code -eq 2) {
    Write-Host "ExitCode 2: CF_DEV_TOOLS changed, retrying once after Unity recompile..."
    $stamp2 = Get-Date -Format "yyyyMMdd-HHmmss"
    $LogFile2 = Join-Path $logsDir "unity-batch-$Command-retry-$stamp2.log"
    $code = Invoke-UnityBatch -LogPath $LogFile2
    $LogFile = $LogFile2
}

Write-Host "ExitCode: $code"
if ($code -ne 0) {
    Write-Host "See log on failure: $LogFile"
}
exit $code
