@echo off

pushd %~dp0

dotnet build -c Release
if %errorlevel% neq 0 (
    popd
    exit /b %errorlevel%
)

pushd Server\bin\Release\net9.0\
start Server.exe
popd

pushd Portal\bin\Release\net9.0\
start Portal.exe
popd

pushd Dashboard\bin\Release\net9.0\
start Dashboard.exe
popd

popd
