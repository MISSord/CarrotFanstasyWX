# Restore tracked files under a path to HEAD (discard local changes).
# Usage: powershell -ExecutionPolicy Bypass -File Tools\restore-scripts-from-git.ps1 [-Path ...] [-Force]

param(
    [string] $Path = "CarrotFantasy/Assets/Scripts",
    [switch] $Force
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$rel = $Path.TrimEnd([char[]]"/\ ")
$target = Join-Path $root $rel
if (-not (Test-Path $target)) {
    Write-Error "Path not found: $target"
    exit 1
}

$inGit = git rev-parse --is-inside-work-tree 2>$null
if ($inGit -ne "true") {
    Write-Error "Not a git repository: $root"
    exit 1
}

Write-Host "Repo: $root"
Write-Host "Running: git restore --source=HEAD --staged --worktree -- $rel"

if (-not $Force) {
    $confirm = Read-Host "Continue? [y/N]"
    if ($confirm -ne "y" -and $confirm -ne "Y") {
        Write-Host "Cancelled."
        exit 0
    }
}

git restore --source=HEAD --staged --worktree -- $rel
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "Done: restored from HEAD -> $rel"
Write-Host "Tip: refresh Unity compile or run git status."
