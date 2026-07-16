#Requires -Version 5.1
# Code hot-update wrapper (no full AB rebuild).
param(
    [string] $Target = "StandaloneWindows64",
    [switch] $Upload,
    [string] $RuntimeEnv,
    [string] $UnityPath,
    [string] $LogFile
)

$invoke = @{
    Command = "codeHotUpdate"
    Target  = $Target
    Upload  = $Upload
}
if (-not [string]::IsNullOrWhiteSpace($RuntimeEnv)) { $invoke.RuntimeEnv = $RuntimeEnv }
if (-not [string]::IsNullOrWhiteSpace($UnityPath)) { $invoke.UnityPath = $UnityPath }
if (-not [string]::IsNullOrWhiteSpace($LogFile)) { $invoke.LogFile = $LogFile }

& (Join-Path $PSScriptRoot "unity-batch.ps1") @invoke
exit $LASTEXITCODE
