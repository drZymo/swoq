# Sioux Weekend of Quest (SWOQ)

Server code for SWOC edition 2025

## Concept

The goal of the player is to finish the quest, which is a series of progressively more difficult 2D mazes.

There are training levels available.

## Architecture

A game can be started with the

Game history is not maintained by the server. Once a game is finished a replay is written to disk and the game process is stopped.

## Prerequisites

For Linux (tested on Arch Linux):

- `dotnet-host` (at least 9.0.1)
- `dotnet-sdk` (at least 9.0.1), which depends on `dotnet-runtime`
- `aspnet-runtime` (at least 9.0.1)
- `aspnet-targeting-pack-bin` (at least 9.0.1)

## Running

You need to have an instance of MongoDB running (on localhost, no authentication).

Run the following programs:

- Portal
- Server

Go to `http://localhost:5080`, create a username and copy the presented user ID
to one of the example bots. Run the example bot.

Run the ReplayViewer, select "Load" and locate the bot's replay (under `Replays`
folder in repo root).

## Generate test coverage

```sh
dotnet test --collect:"Code Coverage;Format=Cobertura"
reportgenerator -targetdir:coveragereport -reports:<path to.cobertura.xml>
```
