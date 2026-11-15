@echo off
setlocal
set "AI_CAD_DOTNET=%~dp0.tools\dotnet\dotnet.exe"
if not exist "%AI_CAD_DOTNET%" set "AI_CAD_DOTNET=%ProgramFiles%\dotnet\dotnet.exe"
if not exist "%AI_CAD_DOTNET%" (
 echo Khong tim thay .NET. Hay kiem tra bo cai AI CAD.
 pause
 exit /b 1
)
set "AI_CAD_LAUNCHER=%~dp0storage\04-builds\artifacts\launcher\AICad.Launcher.dll"
if not exist "%AI_CAD_LAUNCHER%" set "AI_CAD_LAUNCHER=%~dp0artifacts\launcher\AICad.Launcher.dll"
"%AI_CAD_DOTNET%" "%AI_CAD_LAUNCHER%" --cad
set "AI_CAD_EXIT=%ERRORLEVEL%"
if not "%AI_CAD_EXIT%"=="0" (
 echo.
 echo Khong khoi dong duoc AI CAD. Xem data-p0-p6\launcher-last.json.
 pause
)
exit /b %AI_CAD_EXIT%
