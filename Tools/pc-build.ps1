#Requires -Version 5.1
# Thin wrapper: one-click PC player build (dev / prod channel).
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("dev", "prod")]
    [string] $Channel,

    [switch] $SkipGenerate,

    [string] $UnityPath,

    [string] $LogFile
)

$invoke = @{
    Command = "pcBuild"
    Channel = $Channel
    Target  = "StandaloneWindows64"
}
if ($SkipGenerate) { $invoke.SkipGenerate = $true }
if (-not [string]::IsNullOrWhiteSpace($UnityPath)) { $invoke.UnityPath = $UnityPath }
if (-not [string]::IsNullOrWhiteSpace($LogFile)) { $invoke.LogFile = $LogFile }

& (Join-Path $PSScriptRoot "unity-batch.ps1") @invoke
exit $LASTEXITCODE
