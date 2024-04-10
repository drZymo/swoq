# Sioux Weekend of Quest (SWOQ)

Server code for SWOC edition 2025


## Concept

The goal of the player is to finish the quest, which is a series of progressively more difficult 2D mazes.

There are training levels available.

## Architecture

A game can be started with the 

Game history is not maintained by the server. Once a game is finished a replay is written to disk and the game process is stopped.

## Tips

Generate python code with

    python -m grpc_tools.protoc -I. --python_out=. --pyi_out=. --grpc_python_out=. services.proto