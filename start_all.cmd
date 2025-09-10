@echo off

pushd %~dp0

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
start Server.exe %1
popd

pushd Portal\bin\Release\net9.0\win-x64\publish\
start Portal.exe
popd

pushd Dashboard\bin\Release\net9.0\win-x64\publish\
start Dashboard.exe
popd

popd
