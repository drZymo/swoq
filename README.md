# Sioux Weekend of Quest (SWOQ)

Server code for Sioux Weekend of Quest (SWOQ), the challenge for the 2025 edition of the Sioux Weekend of Code.

It is a game played by multiple users in parallel. The goal of the game is to finish the quest, which is a series of progressively more difficult 2D mazes (i.e. levels). Each time a user finishes a level in a quest, their user level is increased as well. Once they reach and finish the last level, the quest is finished and they can be crowned winner of the challenge. Each level is randomly generated, so no single quest is the same. Only one user is allowed to play a quest at a time. Users are placed in a queue if they try to start a quest while one is already active.

Each level can also be played individually in training mode, where the player can try out new strategies before attempting a quest. Multiple training games can be played in parallel without restriction.

## Architecture

gRPC is used as the protocol between the **Server** application and the clients. The protocol and how a game is played are documented [here](Documentation/swoq.md).

A **Portal** application provides a website where users can register, read the documentation, view the current proto file for their level, and view the current ranking.

The **Dashboard** application shows the current state of the Server. It follows the active quests live, and lists the results of previous quests. The current high scores are also displayed live.

Training games are not stored by the server, but Quests are. A replay file is saved while a quest is being played. The **ReplayViewer** can be used to view these files.

## Prerequisites

For Windows:

- Microsoft Visual Studio 2022 (the Community edition suffices)

For Linux (tested on Arch Linux):

- `dotnet-host` (at least 9.0.1)
- `dotnet-sdk` (at least 9.0.1), which depends on `dotnet-runtime`
- `aspnet-runtime` (at least 9.0.1)
- `aspnet-targeting-pack-bin` (at least 9.0.1)

## Running

You need to have an instance of MongoDB running (on localhost, no authentication).

Run the following programs:

- Swoq.Portal
- Swoq.Server
- Swoq.Dashboard

Go to `http://localhost:5080`, create a username, and copy the presented user ID to one of the example bots. Use `localhost:5001` as host. Run the example bot.

Run the ReplayViewer, select "Load", and locate the bot's replay (under the `Replays` folder in the repo root).

For convenience, the `start_all.cmd` script is created for Windows that builds everything and starts the core applications (Server, Portal, and Dashboard).

## Generate test coverage

```sh
dotnet test --collect:"Code Coverage;Format=Cobertura"
reportgenerator -targetdir:coveragereport -reports:<path.to.cobertura.xml>
```
