# 修改 Proto/GameNetwork.proto 后执行：更新 Unity 的 GameNetwork.cs，并编译 CarrotFantasyServer（共用同一份 proto）。
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$cfNetProj = Join-Path $root "Tools\CfNet.ProtoGen\CfNet.ProtoGen.csproj"
$generated = Join-Path $root "Tools\CfNet.ProtoGen\obj\Release\net8.0\GameNetwork.cs"
$dest = Join-Path $root "CarrotFantasy\Assets\Scripts\NetProto\Generated\GameNetwork.cs"
$serverProj = Join-Path $root "CarrotFantasyServer\CarrotFantasyServer.csproj"

dotnet build $cfNetProj -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if (-not (Test-Path $generated)) {
    Write-Error "Missing generated file: $generated (build CfNet.ProtoGen first)"
    exit 1
}
Copy-Item -Force $generated $dest
Write-Host "已更新: $dest"

dotnet build $serverProj -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "CarrotFantasyServer build OK (same GameNetwork.proto)."
