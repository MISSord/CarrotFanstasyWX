@echo off
cd /d "%~dp0"

set LUBAN_DLL=..\Luban\Luban.dll
set OUT_CODE=..\..\CarrotFantasy\Assets\Scripts\Config\Luban
set OUT_DATA=..\..\CarrotFantasy\Assets\Game\Config\Luban

if not exist "%LUBAN_DLL%" (
    echo [ERROR] Luban not found: %LUBAN_DLL%
    pause
    exit /b 1
)

echo Exporting Luban config (client / cs-simple-json / json)...
dotnet "%LUBAN_DLL%" -t client -c cs-simple-json -d json --conf __root__.conf -x "outputCodeDir=%OUT_CODE%" -x "outputDataDir=%OUT_DATA%"
if errorlevel 1 (
    echo [ERROR] Export failed.
    pause
    exit /b 1
)

echo [OK] Code: %OUT_CODE%
echo [OK] Data: %OUT_DATA%
pause
