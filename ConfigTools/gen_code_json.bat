set WORKSPACE=..

set LUBAN_DLL=%WORKSPACE%\Tools\Luban\Luban.dll
set CONF_ROOT=%WORKSPACE%\Tools\Config

dotnet %LUBAN_DLL% ^
    -t client ^
    -c cs-simple-json ^
    -d json  ^
    --conf %CONF_ROOT%\__root__.conf ^
    -x outputCodeDir=%WORKSPACE%/Assets/Scripts/EGamePlay/Config/Luban ^
    -x outputDataDir=..\Assets\Resources\Config\Luban ^