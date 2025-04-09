@echo off

pushd %~dp0ProtoStripper

dotnet restore
dotnet build --no-restore -c Release

"bin\Any CPU\Release\net9.0\ProtoStripper.exe" "%~dp0Interface\swoq.proto" "%~dp0Bot\ExampleCSharp\swoq.proto"
"bin\Any CPU\Release\net9.0\ProtoStripper.exe" "%~dp0Interface\swoq.proto" "%~dp0Bot\ExamplePython\swoq.proto"
"bin\Any CPU\Release\net9.0\ProtoStripper.exe" "%~dp0Interface\swoq.proto" "%~dp0Bot\ExampleTypeScript\protos\swoq.proto"
copy /Y "%~dp0Interface\swoq.proto" "%~dp0\Bot\Fast\swoq.proto"
copy /Y "%~dp0Interface\swoq.proto" "%~dp0\Bot\CompleteTypeScript\protos\swoq.proto"

popd
