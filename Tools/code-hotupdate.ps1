#Requires -Version 5.1
# HybridCLR code hot-update was removed. Lua ships inside the AssetBundle build.
Write-Host "codeHotUpdate was removed with HybridCLR."
Write-Host "Lua hot update is packed by the AssetBundle build:"
Write-Host "  .\Tools\ab-build.ps1"
Write-Host "Editor: AB build profile (Lua bundle is a required step)."
exit 1
