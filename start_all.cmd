@echo off

pushd %~dp0

dotnet restore --locked-mode
if %errorlevel% neq 0 (
    popd
    exit /b %errorlevel%
)

dotnet build -c Release --no-restore
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
