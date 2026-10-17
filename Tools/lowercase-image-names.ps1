<#
.SYNOPSIS
    把 Unity 项目 Assets 下的图片文件名批量改为全小写，并同步 .meta 与序列化引用。

.DESCRIPTION
    为什么需要这个工具：微信小游戏/WebGL 等平台的文件系统区分大小写，而 Windows
    上开发时大小写不敏感，所以 .PNG / Tower_1 这类命名在真机上可能加载失败。

    本工具处理的四件事：
      1. 图片文件改名（含扩展名），并同步 <name>.meta，保证 GUID 不变
      2. 两段式改名，规避 Windows 大小写不敏感导致的"只改大小写失败"
      3. 可选：把 prefab/scene 里 defaultAssetName 的值同步改小写
         （Unity 里 Sprite 名默认等于文件名，改名会连带改掉 sprite 名）
      4. 生成回滚清单，可一键还原

    安全设计：
      - 默认 DryRun，只预览不动文件；必须显式加 -Apply 才执行
      - 执行前检测大小写撞名，有冲突则整批中止
      - 所有改名记录写入清单，-Rollback 可完整撤销

.PARAMETER AssetsRoot
    Assets 目录。默认取脚本所在目录的上一级的 CarrotFantasy\Assets。

.PARAMETER Apply
    真正执行。不加则只演练。

.PARAMETER FixReferences
    同步把 prefab/scene 中 defaultAssetName 的值改为小写（仅当该值精确等于某个被改名文件的原名）。

.PARAMETER Rollback
    按清单撤销上一次 -Apply 的改名。

.PARAMETER Force
    即使检测到大小写撞名也继续（默认中止）。

.EXAMPLE
    # 1) 先看会改什么
    .\lowercase-image-names.ps1

.EXAMPLE
    # 2) 确认无误后执行（含引用修正）
    .\lowercase-image-names.ps1 -Apply -FixReferences

.EXAMPLE
    # 3) 后悔了
    .\lowercase-image-names.ps1 -Rollback
#>
[CmdletBinding()]
param(
    [string]$AssetsRoot,
    [switch]$Apply,
    [switch]$FixReferences,
    [switch]$Rollback,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

# 参与改名的图片扩展名（小写比较）
$ImageExtensions = @(
    '.png','.jpg','.jpeg','.tga','.psd','.tif','.tiff',
    '.exr','.gif','.bmp','.webp','.hdr','.iff','.pict'
)

# ---------- 路径解析 ----------
if (-not $AssetsRoot) {
    $candidate = Join-Path (Split-Path $PSScriptRoot -Parent) 'CarrotFantasy\Assets'
    if (Test-Path -LiteralPath $candidate) { $AssetsRoot = $candidate }
}
if (-not $AssetsRoot -or -not (Test-Path -LiteralPath $AssetsRoot)) {
    throw "找不到 Assets 目录，请用 -AssetsRoot 指定。当前值: '$AssetsRoot'"
}
$AssetsRoot = (Get-Item -LiteralPath $AssetsRoot).FullName
$ManifestPath = Join-Path $PSScriptRoot 'lowercase-image-names.manifest.json'

function Write-Section($text) { Write-Host ""; Write-Host "=== $text ===" -ForegroundColor Cyan }
function Write-Ok($text)      { Write-Host "  [OK]   $text" -ForegroundColor Green }
function Write-Skip($text)    { Write-Host "  [SKIP] $text" -ForegroundColor DarkGray }
function Write-Warn2($text)   { Write-Host "  [WARN] $text" -ForegroundColor Yellow }
function Write-Err2($text)    { Write-Host "  [FAIL] $text" -ForegroundColor Red }

# ---------- 回滚模式 ----------
if ($Rollback) {
    if (-not (Test-Path -LiteralPath $ManifestPath)) { throw "找不到清单: $ManifestPath" }
    $manifest = Get-Content -LiteralPath $ManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    Write-Section "回滚 (清单生成于 $($manifest.createdAt)，共 $($manifest.renames.Count) 项)"
    $failed = 0
    # 逆序撤销，减少中间态冲突
    for ($i = $manifest.renames.Count - 1; $i -ge 0; $i--) {
        $r = $manifest.renames[$i]
        try {
            if (Test-Path -LiteralPath $r.new) {
                Move-Item -LiteralPath $r.new -Destination $r.old -Force
            } else { Write-Warn2 "源不存在，跳过: $($r.new)"; $failed++ }
        } catch { Write-Err2 "$($r.new) -> $($r.old) : $($_.Exception.Message)"; $failed++ }
    }
    Write-Section "回滚完成（失败 $failed 项）"
    if ($failed -eq 0) { Remove-Item -LiteralPath $ManifestPath -Force -ErrorAction SilentlyContinue }
    return
}

# ---------- 1. 盘点 ----------
Write-Section "盘点"
$allImages = Get-ChildItem -LiteralPath $AssetsRoot -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $ImageExtensions -contains $_.Extension.ToLower() }
Write-Host "  图片总数: $($allImages.Count)"

$targets = @($allImages | Where-Object { $_.Name -cne $_.Name.ToLower() })
Write-Host "  需要改名: $($targets.Count)"

if ($targets.Count -eq 0) { Write-Ok "全部已是小写，无需处理"; return }

# 大小写撞名检测：同目录下改小写后是否与现有文件重名
$collisions = @()
foreach ($t in $targets) {
    $dir  = Split-Path $t.FullName -Parent
    $newN = $t.Name.ToLowerInvariant()
    if ($newN -eq $t.Name) { continue }
    $dest = Join-Path $dir $newN
    # 目标已存在，且不是"自己改名自己"
    if ((Test-Path -LiteralPath $dest) -and ($dest -cne $t.FullName)) { $collisions += "$($t.FullName) -> $dest" }
    $destMeta = $dest + '.meta'
    if ((Test-Path -LiteralPath $destMeta) -and ($destMeta -cne ($t.FullName + '.meta'))) { $collisions += "$($t.FullName).meta -> $destMeta" }
}
if ($collisions.Count -gt 0) {
    Write-Section "★ 检测到 $($collisions.Count) 处大小写撞名"
    $collisions | Select-Object -First 20 | ForEach-Object { Write-Err2 $_ }
    if (-not $Force) {
        Write-Host ""
        Write-Host "  为安全起见已中止。请先手动处理上述冲突，或加 -Force 强行继续（不推荐）。" -ForegroundColor Red
        return
    }
    Write-Warn2 "已指定 -Force，继续执行"
} else { Write-Ok "无大小写撞名冲突" }

# .meta 完整性
$missingMeta = @($targets | Where-Object { -not (Test-Path -LiteralPath ($_.FullName + '.meta')) })
if ($missingMeta.Count -gt 0) {
    Write-Warn2 "$($missingMeta.Count) 个文件缺少 .meta（改名后 Unity 会重新生成，可能导致引用丢失）"
} else { Write-Ok "全部都有同名 .meta" }

# ---------- 2. 预览 ----------
Write-Section "改名预览（前 15 条 / 共 $($targets.Count) 条）"
$targets | Select-Object -First 15 | ForEach-Object {
    Write-Host ("  {0}  ->  {1}" -f $_.Name, $_.Name.ToLowerInvariant())
}
if ($targets.Count -gt 15) { Write-Host "  ... 其余 $($targets.Count - 15) 条省略" }

# ---------- 3. 引用影响分析 ----------
$renameMap = @{}   # 原名(不含扩展名) -> 新名(不含扩展名)
foreach ($t in $targets) {
    $oldBase = [IO.Path]::GetFileNameWithoutExtension($t.Name)
    $newBase = [IO.Path]::GetFileNameWithoutExtension($t.Name.ToLowerInvariant())
    if ($oldBase -cne $newBase) { $renameMap[$oldBase] = $newBase }
}

$refFiles = @()
$refHitCount = 0
if ($renameMap.Count -gt 0) {
    Write-Section "序列化引用影响（defaultAssetName）"
    $candidates = Get-ChildItem -LiteralPath $AssetsRoot -Recurse -File -Include *.prefab,*.unity,*.asset -ErrorAction SilentlyContinue
    foreach ($f in $candidates) {
        $text = [IO.File]::ReadAllText($f.FullName)
        $hits = @()
        foreach ($m in [regex]::Matches($text, '(?m)^(\s*defaultAssetName:\s*)(\S+)\s*$')) {
            $val = $m.Groups[2].Value
            if ($renameMap.ContainsKey($val)) { $hits += $val }
        }
        if ($hits.Count -gt 0) {
            $refFiles += $f
            $refHitCount += $hits.Count
        }
    }
    Write-Host "  受影响文件数: $($refFiles.Count)"
    Write-Host "  受影响条数  : $refHitCount"
    if ($refHitCount -gt 0) {
        if ($FixReferences) {
            Write-Ok "已指定 -FixReferences，将同步改写这些值"
        } else {
            Write-Warn2 "未指定 -FixReferences —— 这些引用在改小写后会失效！"
            Write-Warn2 "Unity 里 Sprite 名默认等于文件名，改名即改 sprite 名。"
            Write-Warn2 "强烈建议加 -FixReferences 一起执行，或事后在 Unity 里手动重绑。"
        }
    }
}

# ---------- 4. DryRun 终止 ----------
if (-not $Apply) {
    Write-Section "DryRun 结束（未改动任何文件）"
    Write-Host "  确认无误后执行："
    Write-Host "    .\lowercase-image-names.ps1 -Apply -FixReferences" -ForegroundColor Green
    return
}

# ---------- 5. 执行改名 ----------
Write-Section "执行改名"
$manifest = [ordered]@{
    createdAt = (Get-Date).ToString('o')
    assetsRoot = $AssetsRoot
    renames   = @()
}
$okCount = 0
$failCount = 0
$tmpSuffix = '.__lc_tmp'

foreach ($t in $targets) {
    $dir     = Split-Path $t.FullName -Parent
    $oldPath = $t.FullName
    $newName = $t.Name.ToLowerInvariant()
    $newPath = Join-Path $dir $newName
    if ($newName -ceq $t.Name) { continue }

    $oldMeta = $oldPath + '.meta'
    $newMeta = $newPath + '.meta'
    $hasMeta = Test-Path -LiteralPath $oldMeta

    # 两段式：先挪到临时名（与新旧都不同），再挪到目标名
    $tmpPath = $oldPath + $tmpSuffix
    $tmpMeta = $oldMeta + $tmpSuffix
    try {
        Move-Item -LiteralPath $oldPath -Destination $tmpPath
        if ($hasMeta) { Move-Item -LiteralPath $oldMeta -Destination $tmpMeta }
        Move-Item -LiteralPath $tmpPath -Destination $newPath
        if ($hasMeta) { Move-Item -LiteralPath $tmpMeta -Destination $newMeta }

        $manifest.renames += [ordered]@{ old = $oldPath; new = $newPath; hadMeta = $hasMeta }
        $okCount++
    } catch {
        Write-Err2 "$($t.Name) : $($_.Exception.Message)"
        # 尽量回退到原名，避免留半成品
        if (Test-Path -LiteralPath $tmpPath) { try { Move-Item -LiteralPath $tmpPath -Destination $oldPath -Force } catch {} }
        if ($hasMeta -and (Test-Path -LiteralPath $tmpMeta)) { try { Move-Item -LiteralPath $tmpMeta -Destination $oldMeta -Force } catch {} }
        $failCount++
    }
}
Write-Ok "改名成功 $okCount 项，失败 $failCount 项"

# ---------- 6. 修正引用 ----------
if ($FixReferences -and $renameMap.Count -gt 0 -and $refFiles.Count -gt 0) {
    Write-Section "修正 defaultAssetName 引用"
    $fixedFiles = 0
    $utf8NoBom  = New-Object System.Text.UTF8Encoding($false)
    foreach ($f in $refFiles) {
        try {
            $bytes = [IO.File]::ReadAllBytes($f.FullName)
            $hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
            $text  = $utf8NoBom.GetString($bytes)
            if ($hasBom) { $text = $text.Substring(1) }

            $changed = $false
            $newText = [regex]::Replace($text, '(?m)^(\s*defaultAssetName:\s*)(\S+)\s*$', {
                param($m)
                $val = $m.Groups[2].Value
                if ($renameMap.ContainsKey($val)) {
                    return $m.Groups[1].Value + $renameMap[$val]
                }
                return $m.Value
            })
            if ($newText -cne $text) { $changed = $true }

            if ($changed) {
                $enc = if ($hasBom) { New-Object System.Text.UTF8Encoding($true) } else { $utf8NoBom }
                [IO.File]::WriteAllText($f.FullName, $newText, $enc)
                $fixedFiles++
            }
        } catch { Write-Err2 "改写失败 $($f.Name): $($_.Exception.Message)" }
    }
    Write-Ok "已修正 $fixedFiles 个文件"
}

# ---------- 7. 写清单 + 校验 ----------
try {
    ($manifest | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath $ManifestPath -Encoding UTF8
    Write-Ok "回滚清单已写入: $ManifestPath"
} catch { Write-Warn2 "清单写入失败: $($_.Exception.Message)" }

Write-Section "结果校验"
$remain = @(Get-ChildItem -LiteralPath $AssetsRoot -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $ImageExtensions -contains $_.Extension.ToLower() -and $_.Name -cne $_.Name.ToLower() })
if ($remain.Count -eq 0) { Write-Ok "所有图片文件名已全小写" } else { Write-Warn2 "仍有 $($remain.Count) 个文件含大写（可能是失败项）" }

$noMeta = @(Get-ChildItem -LiteralPath $AssetsRoot -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $ImageExtensions -contains $_.Extension.ToLower() } |
    Where-Object { -not (Test-Path -LiteralPath ($_.FullName + '.meta')) })
if ($noMeta.Count -eq 0) { Write-Ok ".meta 全部齐全" } else { Write-Err2 "$($noMeta.Count) 个图片缺少 .meta" }

Write-Section "后续步骤"
Write-Host "  1) 仓库 core.ignorecase=true，仅大小写变更可能不显示，请执行："
Write-Host "       git add -A" -ForegroundColor Green
Write-Host "       git status" -ForegroundColor Green
Write-Host "  2) 用 Unity 打开工程，等待重新导入完成，检查 Sprite 引用是否正常"
Write-Host "  3) 如需撤销：.\lowercase-image-names.ps1 -Rollback"
