#Requires -Version 5.1
# Full AssetBundle build wrapper.
param(
    [string] $Target = "StandaloneWindows64",
    [switch] $Upload,
    [switch] $ForceRebuild,
    [switch] $CopyStreaming,
    [string] $RuntimeEnv,
    [string] $UnityPath,
    [string] $LogFile
)

$invoke = @{
    Command       = "abBuild"
    Target        = $Target
    Upload        = $Upload
    ForceRebuild  = $ForceRebuild
    CopyStreaming = $CopyStreaming
}
if (-not [string]::IsNullOrWhiteSpace($RuntimeEnv)) { $invoke.RuntimeEnv = $RuntimeEnv }
if (-not [string]::IsNullOrWhiteSpace($UnityPath)) { $invoke.UnityPath = $UnityPath }
if (-not [string]::IsNullOrWhiteSpace($LogFile)) { $invoke.LogFile = $LogFile }

& (Join-Path $PSScriptRoot "unity-batch.ps1") @invoke
exit $LASTEXITCODE
