@echo off

pushd %~dp0

set ReplayStorage__Folder=%~dp0Replays\

taskkill /f /im Swoq.Server.exe
taskkill /f /im Swoq.Portal.exe
taskkill /f /im Swoq.Dashboard.exe

dotnet restore --locked-mode
if %errorlevel% neq 0 (
    popd
    exit /b %errorlevel%
)

dotnet publish -c Release --no-restore -r win-x64
if %errorlevel% neq 0 (
    popd
    exit /b %errorlevel%
)

pushd Server\bin\Release\net9.0\win-x64\publish\
start Swoq.Server.exe %1
popd

pushd Portal\bin\Release\net9.0\win-x64\publish\
start Swoq.Portal.exe
popd

pushd Dashboard\bin\Release\net9.0\win-x64\publish\
start Swoq.Dashboard.exe
popd

popd
