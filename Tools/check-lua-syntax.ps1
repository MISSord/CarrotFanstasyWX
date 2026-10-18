<#
.SYNOPSIS
    用工程自带的 xlua.dll 对所有 .lua 脚本做语法校验（只编译，不执行）。

.DESCRIPTION
    为什么不用 lua.exe：工程里没有 Lua 解释器，而 xlua.dll 导出了标准 Lua C API
    （xluaL_loadbuffer），用它校验得到的结论与运行时完全一致。
    这里是 P/Invoke 调用，不依赖 Unity 是否在运行。

.PARAMETER LuaRoot
    Lua 源码目录（相对工程根或绝对路径）。默认 Assets/Game/LuaScripts

.PARAMETER XluaDll
    xlua.dll 路径（相对工程根或绝对路径）。默认 Assets/Plugins/x86_64/xlua.dll

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Tools\check-lua-syntax.ps1

.NOTES
    退出码：0 = 全部通过；1 = 有语法错误或环境问题（可用于 CI 拦截）
#>
param(
    [string]$LuaRoot = "Assets/Game/LuaScripts",
    [string]$XluaDll = "Assets/Plugins/x86_64/xlua.dll"
)

$ErrorActionPreference = "Stop"

function Resolve-ProjectPath {
    param([string]$Path, [string]$Root)
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $Root $Path)
}

# 工程根：从脚本位置向上逐层查找
#   命中 <dir>\Assets          -> 该目录
#   命中 <dir>\CarrotFantasy\Assets -> <dir>\CarrotFantasy
$projectRoot = $null
$dir = $PSScriptRoot
while ($dir) {
    if (Test-Path (Join-Path $dir "CarrotFantasy\Assets")) { $projectRoot = (Join-Path $dir "CarrotFantasy"); break }
    if (Test-Path (Join-Path $dir "Assets"))             { $projectRoot = $dir; break }
    $parent = Split-Path $dir -Parent
    if (-not $parent -or $parent -eq $dir) { break }
    $dir = $parent
}
if (-not $projectRoot) { $projectRoot = (Get-Location).Path }
$absLuaRoot = Resolve-ProjectPath $LuaRoot $projectRoot
$absDll     = Resolve-ProjectPath $XluaDll $projectRoot

Write-Host "工程根  : $projectRoot"
Write-Host "Lua 目录: $absLuaRoot"
Write-Host "xlua.dll: $absDll"

if (-not (Test-Path $absLuaRoot)) { Write-Error "Lua 目录不存在: $absLuaRoot"; exit 1 }
if (-not (Test-Path $absDll))     { Write-Error "xlua.dll 不存在: $absDll";     exit 1 }

$code = @"
using System;
using System.IO;
using System.Runtime.InteropServices;

public static class LuaSyntaxCheck
{
    [DllImport(@"$absDll", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr luaL_newstate();

    [DllImport(@"$absDll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void lua_close(IntPtr L);

    [DllImport(@"$absDll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int xluaL_loadbuffer(IntPtr L, byte[] buff, int size, string name);

    [DllImport(@"$absDll", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr lua_tolstring(IntPtr L, int index, out IntPtr len);

    public static bool SelfTest()
    {
        IntPtr L = luaL_newstate();
        if (L == IntPtr.Zero) return false;
        lua_close(L);
        return true;
    }

    public static string Check(string path)
    {
        byte[] src = File.ReadAllBytes(path);
        if (src.Length >= 3 && src[0] == 0xEF && src[1] == 0xBB && src[2] == 0xBF)
        {
            byte[] stripped = new byte[src.Length - 3];
            Array.Copy(src, 3, stripped, 0, stripped.Length);
            src = stripped;
        }

        IntPtr L = luaL_newstate();
        try
        {
            int rc = xluaL_loadbuffer(L, src, src.Length, path);
            if (rc == 0) return null;
            IntPtr len;
            IntPtr p = lua_tolstring(L, -1, out len);
            return p == IntPtr.Zero ? ("load failed, code=" + rc) : Marshal.PtrToStringAnsi(p, (int)len);
        }
        finally
        {
            lua_close(L);
        }
    }
}
"@

Add-Type -TypeDefinition $code -Language CSharp

if (-not [LuaSyntaxCheck]::SelfTest()) {
    Write-Error "xlua.dll 加载失败（检查位数：需 64 位 PowerShell）"
    exit 1
}

$files = Get-ChildItem -Path $absLuaRoot -Recurse -Filter *.lua -File | Sort-Object FullName
if ($files.Count -eq 0) { Write-Host "未找到 .lua 文件" -ForegroundColor Yellow; exit 0 }

$fail = 0
foreach ($f in $files) {
    $rel = $f.FullName.Substring($absLuaRoot.Length).TrimStart('\', '/')
    $err = [LuaSyntaxCheck]::Check($f.FullName)
    if ($null -eq $err) {
        Write-Host ("  OK   {0}" -f $rel) -ForegroundColor DarkGray
    } else {
        $fail++
        Write-Host ("  FAIL {0}" -f $rel) -ForegroundColor Red
        Write-Host ("       {0}" -f ($err -replace "`r?`n", " ")) -ForegroundColor Red
    }
}

Write-Host ""
if ($fail -eq 0) {
    Write-Host ("全部通过：{0} 个脚本" -f $files.Count) -ForegroundColor Green
    exit 0
} else {
    Write-Host ("失败 {0} / {1} 个脚本" -f $fail, $files.Count) -ForegroundColor Red
    exit 1
}