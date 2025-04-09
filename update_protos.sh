#!/bin/sh
set -e

root=$(dirname $(readlink -f "$0"))

cd "${root}/ProtoStripper"
dotnet restore
dotnet build --no-restore -c Release

"./bin/Any CPU/Release/net9.0/ProtoStripper" "${root}/Interface/swoq.proto" "${root}/Bot/ExampleCSharp/swoq.proto"
"./bin/Any CPU/Release/net9.0/ProtoStripper" "${root}/Interface/swoq.proto" "${root}/Bot/ExamplePython/swoq.proto"
"./bin/Any CPU/Release/net9.0/ProtoStripper" "${root}/Interface/swoq.proto" "${root}/Bot/ExampleTypeScript/protos/swoq.proto"
cp "${root}/Interface/swoq.proto" "${root}/Bot/Fast/swoq.proto"
cp "${root}/Interface/swoq.proto" "${root}/Bot/CompleteTypeScript/protos/swoq.proto"
