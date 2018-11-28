@echo on

rem working directory
set workdir=%~dp0
cd %workdir%

MonoPatch.exe -out %workdir%/../ -scp modify.scp %workdir%/../dll/PluginFramework.dll %workdir%/../dll/Cs2LuaScript.dll
copy /Y %workdir%\..\*.dll d:\work\Client\proj.unity\Assets\Plugins\game\
rem copy /Y %workdir%\..\*.mdb d:\work\Client\proj.unity\Assets\Plugins\game\
