# Restore tracked files under a path to HEAD (discard local changes).
# Usage: powershell -ExecutionPolicy Bypass -File Tools\restore-scripts-from-git.ps1 [-Path ...] [-Force]
# Non-interactive (Cursor tasks / CI): always pass -Force.

param(
    [string] $Path = "CarrotFantasy/Assets/Scripts",
    [switch] $Force
)

$ErrorActionPreference = "Stop"

if (-not $PSScriptRoot) {
    Write-Error "PSScriptRoot is empty; invoke with: powershell -ExecutionPolicy Bypass -File `"<path>\restore-scripts-from-git.ps1`""
    exit 1
}

Push-Location $PSScriptRoot
try {
    $root = (& git rev-parse --show-toplevel 2>$null).Trim()
} finally {
    Pop-Location
}

if (-not $root -or ($LASTEXITCODE -ne 0)) {
    Write-Error "Could not find git repository root (is git installed and on PATH?). Script dir: $PSScriptRoot"
    exit 1
}

Set-Location $root

$rel = $Path.TrimEnd([char[]]"/\ ")
$target = Join-Path $root $rel
if (-not (Test-Path $target)) {
    Write-Error "Path not found: $target"
    exit 1
}

Write-Host "Repo: $root"
Write-Host "Running: git restore --source=HEAD --staged --worktree -- $rel"

if (-not $Force) {
    try {
        $confirm = Read-Host "Continue? [y/N]"
    } catch {
        Write-Error "Cannot prompt (non-interactive?). Re-run with -Force. Details: $_"
        exit 1
    }
    if ($confirm -ne "y" -and $confirm -ne "Y") {
        Write-Host "Cancelled."
        exit 0
    }
}

git restore --source=HEAD --staged --worktree -- $rel
if ($LASTEXITCODE -ne 0) {
    Write-Warning "git restore failed (requires Git 2.23+). Trying: git checkout HEAD + git reset HEAD ..."
    git checkout HEAD -- $rel
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
    git reset HEAD -- $rel
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

Write-Host "Done: restored from HEAD -> $rel"
Write-Host "Tip: refresh Unity compile or run git status."
