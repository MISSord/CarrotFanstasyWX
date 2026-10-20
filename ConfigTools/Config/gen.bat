@echo off
cd /d "%~dp0"

set LUBAN_DLL=..\Luban\Luban.dll
set OUT_CS_CODE=..\..\CarrotFantasy\Assets\Scripts\Config\Luban
set OUT_JSON=..\..\CarrotFantasy\Assets\Game\Config\Luban
set OUT_LUA_CODE=..\..\CarrotFantasy\Assets\Game\LuaScripts\gen\luban
set OUT_LUA_DATA=..\..\CarrotFantasy\Assets\Game\LuaScripts\gen\luban_data

if not exist "%LUBAN_DLL%" (
    echo [ERROR] Luban not found: %LUBAN_DLL%
    pause
    exit /b 1
)

if not exist "%OUT_LUA_CODE%" mkdir "%OUT_LUA_CODE%"
if not exist "%OUT_LUA_DATA%" mkdir "%OUT_LUA_DATA%"

echo Exporting Luban config (client / cs-simple-json+json / lua-lua+lua)...
dotnet "%LUBAN_DLL%" -t client -c cs-simple-json -c lua-lua -d json -d lua --conf __root__.conf -x "cs-simple-json.outputCodeDir=%OUT_CS_CODE%" -x "json.outputDataDir=%OUT_JSON%" -x "lua-lua.outputCodeDir=%OUT_LUA_CODE%" -x "lua.outputDataDir=%OUT_LUA_DATA%"
if errorlevel 1 (
    echo [ERROR] Export failed.
    pause
    exit /b 1
)

echo [OK] C# code: %OUT_CS_CODE%
echo [OK] JSON:    %OUT_JSON%
echo [OK] Lua code:%OUT_LUA_CODE%
echo [OK] Lua data:%OUT_LUA_DATA%
pause
