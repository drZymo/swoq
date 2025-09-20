#!/bin/bash -eux

export ReplayStorage__Folder=$(pwd)/Replays

killall -q -w Swoq.Server Swoq.Portal Swoq.Dashboard || true

dotnet restore --locked-mode
dotnet publish -c Release --no-restore -r linux-x64

function start () {
    pushd $1
    [ -x $2 ]
    
    ./$2 > /tmp/$2.log &
    popd
}

start Server/bin/Release/net9.0/linux-x64/publish Swoq.Server
start Portal/bin/Release/net9.0/linux-x64/publish Swoq.Portal
start Dashboard/bin/Release/net9.0/linux-x64/publish Swoq.Dashboard
