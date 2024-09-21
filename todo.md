# TODO

- [ ] Show current game sessions
- [ ] Quest finished screen in dashboard
- [ ] Quest finished mark in high score
- [.] User portal
  - [X] user registration
  - [X] Proto file hosting per level
  - [ ] Current level
  - [ ] API description per level
- [.] Allow more than 1 quest at a time in case it is needed
- [ ] Hardcode dashboard colors
- [ ] Optmize performance of Game.cs
- [ ] Auto hide inactive quests in dashboard
- [ ] Installer/Zip file for ReplayViewer
- [ ] Differentiate the names of Swoq.Server.GameState and Swoq.InfraUI.Models.GameState
- [ ] Rolling ball
- [ ] Lore in the proto files
- [ ] Tileset matching lore (exit should not be treasure)
- [?] Slower with boulder in inventory. 50% change movement is executed.
- [?] Remember health at start of level
- [ ] Autotile in map viewer
- [ ] Single build output/publish folder for server and questdashboard
- [ ] Auto start dashboard from server
- [ ] Publish a single-file app
- [ ] Sync appsettings for production/development
- [ ] Tile size (w/h) dynamic in InfraUI
- [ ] Extend .editorconfig
- [ ] Refactor 3 enemy + boss handling in maps and mapbuilder

## Done
- [X] Act vs Move/Use
- [X] Door open/closed states
- [X] Map generator
- [X] Two players
- [X] Pressure plates
- [X] Quest game
- [X] Combat
- [X] Extra health
- [X] Replay storage
- [X] Replay viewer
- [X] Sword separate inventory
- [X] Health in state
- [X] Map editor writeablebitmap
- [X] Two player bot
- [X] Always send state, even on failures
- [X] Stop game when one of two players dies
- [X] Game Time-To-Live (TTL) for cleanup
- [X] Quest statistics
- [X] Dashboard with player stats
- [X] One quest at a time
- [X] Quest monitoring
- [X] Correct shutdown of open grpc streams
- [X] Show result of request in GameStateView
- [X] Truncate game time / detect stuck/idle
- [X] Player name (big) in quest dashboard
- [X] Unit tests for waiting queue, using mocked datetime
- [X] Quest queue in dashboard
- [X] Player name in replay so it is visible in viewer
- [X] File browser in replay viewer
- [X] Random idle for enemy
- [X] Use Avalonia for all UIs so it is multi-platform
- [X] BUG: Refresh writeable bitmap
- [X] Full screen with Avalonia
- [X] Reduce code duplication between dashboard and replay viewer (create game state)
- [X] Boulders.
- [X] Levels
  - [X] Loot drop sword (auto?)
  - [X] Test door kills enemy
  - [X] Big boss
  - [X] Boulder exit kill
  - [X] Treasure
  - [X] Treasure exit
- [X] Unit tests
  - [X] Boss health and damage
  - [X] Exit tests
- [X] Health and sword could be one same location overwriting eachother.
- [X] Enemy does not attack immediately when first standing next to player. Will make it possible to run past enemy without being attacked.
- [X] Enemy keeps on following even if player out of sight, but with more idle actions.
- [X] Colored pressure plates
- [X] tiles.png path relative to exe
- [X] Idle detection when placing/picking up boulder does not work. Add position change check.
- [X] Test other colored pressure plates
- [X] Level not available => insufficient player level
- [X] More tests
  - [X] invalid uses
  - [X] enemy chasing out of view
  - [X] player crushed by door
  - [X] quest progress (replay with quicker time improves stats)
- [X] Fix boulder handling in bot
- [X] BUG: Move not allowed does not trigger inactivity
- [X] Distinguish player in game and player on interface. User vs Player
  - Player -> User
  - GamePlayer -> Player, GameEnemy -> Enemy
- [X] Remove user registration from proto (move to portal)
- [X] Reuse database stuff betwen portal and server
- [X] Reorder proto files for incremental features
  - [X] Player state optional fields
- [X] Order: width/height, x/y in proto
- [X] Example bot Python
- [X] Example bot C#
- [X] Extra one player combat levels before introducing second player
  - [X] New levels
  - [X] Prevent sword drop by enemy until two players
  - [X] Update proto files levels (automatic search from quest replay?)
  - [X] Enemy drop loot near player
- [X] Random player, random level train script to test load
- [X] Duration test for mapgenerator
- [X] BUG: Fix multi-threaded map generator


# Design decisions

## Act vs Move/Use

Combine Move and Use functions into one Act/Step function. One parameter (enum) for the type of action to perform and one parameter for the direction. Then later, when two players are introduced the message can be extended with two optional fields for action/direction of player 2.

## Door open/closed states

Instead of removing a door from the map it should switch between an open and closed state, e.g. DoorRedClosed, DoorRedOpen. When open it can be walked on (is not a wall).

This is needed to allow closing doors with pressure plates.

## Map generator

A generator for maps.

- Add rooms
- Connect rooms
- Add key locker rooms
- Add pressure plates

## Two players

First one player. Then a level with a second player in a prison that player 1 needs to open first. Both players need to reach exit before level is finished.

The Act request contains action1/direction1 and action2/direction2.
In the state is the position and inventory of both players as well.


## Pressure plates

When a player stands on it a door is opened. If left, the door closes.
In two player game: a red door without a key, the key is in the room behind the door. A black door with a pressure plate. One player needs to remain on plate so door is opened. Other player can then pickup key and open the door for the other player.


## Combat

- Enemies
- Sword
- Health
- Armor

Need two players to attack simultanously to defeat enemy. One player dies is end of game. One-on-one enemy will win.

E.g. hit is 1 health. Both players have 100 health. Enemy has 120 health.
Need sword to be able to hit. So two swords per level.
Each turn players get hit by enemy if in adjacent cell. Every use will hit enemy.
Enemy walks 1 cell every two ticks. Players can walk 1 cell per tick. So easy to outrun.

Pickup armor to add 50 health. Makes it possible for one player to kill enemy.

Enemy has visibility range of 5. Will not follow (remain stationary) if out of this range.

Enemies can have inventory which will drop when it dies. Like keys, health or armor.

1 Player with armor is 150 health, so it can defeat one enemy and have 30 health left. Extra armor not enough to be strong enough for another fight. So one player can defeat one enemy.

TODO: picking up sword will block inventory for picking up keys. So if enemy is killed and drops a key, then you cannot pick it up. Should the sword break/disappear when enemy is killed?

Simpler score? Player health = 5, Enemy health = 6. With armor Player health = 8

Or always a sword present? So you can always use on an enemy to attack?

## Replay storage

Binary format.
Actions plus observations (surroundings) are stored, i.e. most of what is communicated.
Whole map of each time step is stored as well.

    1 byte map height
    1 byte map width
    1 byte level
    1 byte visibility range
    2 bytes nr time steps
    X time steps
        1 byte action performed (0 or -1 at first step)
        1 byte result
        1 byte player pos y
        1 byte player pos x
        X bytes surroundings
        X bytes map

Decided to store protobuf messages between server and player.

## Replay viewer

Show replays step by step. Allows scrolling.
Can watch folder.
Auto play mode: auto plays latest game. When finished plays next latest or loops the same again.



## Levels

Basic setup:
First one player, later 2 player.
Progressive introduction of features. When feature is introduced it is done in a simple setting. Features are introduced in the following order:
1. Maze
2. Doors
3. Boulders
4. Pressure plate with boulders
5. Combat
6. Two players
7. Two player combat
8. Finale

Once all features are introduced, the levels are constructed with more and more difficult combinations.

0. **(Maze)** Simple one room map. One player, no doors.
1. **(Maze)** More difficult map with maze. One player, no doors. (Standard maze.)
2. **(Doors)** Maze with door in front of exit. Key is placed far away from player and door.
3. **(Doors)** One locker room. Key to exit door is placed in a room with another door. That key is in the open.
4. **(Doors)** Two locker rooms. Repeat. 3 doors in total.
5. **(Doors)** Double-door locker room. Two doors to enter room with exit key. Both keys are in the open. Key for inner door is close to the player at startup, so it can be picked up accidentally. Outer door key is far away from room and player.
6. **(Boulders)** Standard maze with boulders in front of exit. Pick up boulder to reach exit. But drop it before exiting.
7. **(Pressure plate with boulders)** Pressure plate in front of exit with door. Several boulders scattered through maze. Door is visible from pressure plate, so it can be observed to open.
8. **(Combat)** First enemy. In room of exit. Run to exit. No sword.
9. **(Combat)** Lure. One enemy with key to exit door. In a room before exit. Room is locked with second door with pressure plate in fron. Crush enemy with door to get key. No swords.
10. **(Combat)** First combat. Locked exit. One enemy with key to exit door. One sword and health in initial room.
11. **(Combat)** Two enemies. First enemy drops key for room with second enemy. Second enemy has key for exit. 3 health needed to win from two enemies.
12. **(Two players)** Prison. One room with a door that holds the second player. Door is guarded by an enemy. Sword and health somewhere in map. Enemy has key for door. Exit is open, so it tempted to leave without freeing second player.
13. **(Two players)** Simple two locker rooms, but now with 2 players. Correct player must pick up keys and open doors.
14. **(Two players)** Double-door locker room. With two players. Again right key must be picked up first.
15. **(Two players)** Pressure plate wall. Two sided level with two corridors. One locked with pressure plate door, other with regular door. Pressure plate on left side, key to door on the right side. Must work together to open regular door. Exit in the open, so it is tempting to enter without helping other.
16. **(Two players)** Double pressure plate. Double-door locker room. Pressure plate for both doors. One boulder in the level. Key for exit door in locker. One player needs to stand on pressure plate, other needs a boulder on it.
17. **(Two player combat)** Two sided maze with door. Left side has swords, no health. Right side has one enemy. Work together to kill enemy with key to exit.
18. **(Two player combat)** Run for sword. No more left/right sides. Enemy is in the room next to the spawn point, swords are far away on the map. Enemy has key for exit door.
19. **(Two player combat)** Two enemies. Split maze. One enemy on left side in front of tunnel door. Other in front of exit door. One sword and health in left side. Extra heath in right side. First enemy drops sword for second player. Second enemy drops key for exit door. Total player health = 5+3 + 5+3 = 16. Total enemy health = 12. One player total health = 5+3+3 = 11, so need two player interaction. One player needs to collect sword and health first then kill enemy so second player can grab the sword.
20. **(Two player combat)** Separation. At start, pressure plate to open one door. Player1 needs to step on it so Player2 can enter next room. In that room another pressure plate to open other door in start room. Player2 needs to step on it so Player1 can enter other room. Now both players are in a separate part of the map, where the both have to kill their own enemy. Each enemy leaves a key to enter the final part of the map where both players are joined again. One final enemy with key to exit.
21. **(Finale)** Grand desert. Double pressure plate locker room with two swords and two health. Players have to take turns getting swords and health. 2x health and boulder scattered around map. Pre-exit room with two guards. One extra guard in exit room with key for exit.
22. **(Finale)** Crush. One enemy (with lots of health and damage), swords and health in level, but still not enough to defeat boss. Corridor/room with pressure plate controlled door wall. One player must lure the boss on the plate. Other player must stand on pressure plate (somewhere far away) and step off when boss is on the door position. Door is closed and kills boss. Boss loot is key for exit door and two big treasures and are placed next to closed door. Without treasure in inventory player is killed when leaving.

### Boss

Boss moves only once every odd tick, so it is slower than normal. It is extra strong. Two players with extra health cannot kill it. One attack kills player instantly. So, 100 health and 20 damage.


## Replay viewer

Should allow live viewing. But how to handle multiple games at the same time?
Or should we allow only one quest at a time and only live viewing of quests?


## Dashboard

Web page hosted by server that shows player statistics.
- Time line with player progression.
- Ordered list of players with levels
- Nr ticks needed to finish quest

## Quest monitoring

Live viewing of current progress. Show current map. No timer based replay. Always show current state. Could mean skipping a few steps.

## Proto file hosting per level

Features are enabled per level, so proto file must be extended as well.
Makes it a surprise to player when a new feature is unlocked.

## Quest statistics

Number of ticks needed to finish quest.
Wall clock time needed to finish quest.

Winner is one who finished with lowest number of ticks or shortest wall clock time if tied.


## One quest at a time

Makes it more a competition.
Allow starting a quest, but return an error code that indicates the game is queued.
Retry start will make sure that the previous queued game is checked.
If it is your turn, then start and act are allowed to proceed.
Timeout needed. If it is a game's turn and last event is more than X seconds ago, then skip game and continue to next.

Need to know current active quest and a list of queued quests.
On start always add to queue first. Then cleanup all items in queue.

If action comes in for timed out quest special return needed.


## Truncate game time / detect stuck/idle

If a game does not proceeed in a certain time it has to stop. In particular quests, otherwise other players cannot compete.


## Remember health at start of level

Instead of resetting health at start of a level use the health at the end of the previous level.
Remember these healths of the best quest, and use that during training as well.
This way it becomes important to not lose too much health in order to reach the end.

Each enemy has 5 health, so it can deal 5 damage. Total player health plus all health that can be obtained must sum up to total damage that can be dealt plus some margin.

But, this might make it too difficult.


## Random idle for enemy

Let enemies once every action stop interacting. This randomizes their moves a little, preventing deadlocks with player


## Boulders

Can be picked up and placed with use command. But once picked up, the inventory is blocked for picking up keys.

Used to block paths.

Placed on pressure plate will leave it pressed.

## Unit tests

- [X] Combat
  - [X] 1 player without health cannot win
  - [X] 1 player with health can win
  - [X] 2 player vs 1 enemy no extra health can win
- [X] Doors
  - [X] Door cannot be opened without key
  - [X] Door can be opened with key
- [ ] Pressure plate
  - [X] Stand will open, leave will close
  - [X] Boulder will open, remove will close
  - [ ] Close door kills player
- [ ] Inactive time
- [ ]


# Lore in the proto files

Add a short story in the proto file for each level about the quest.

Main character: Female, daughter of town baker. Adventurous, is rather outside than in the bakery.
Secondary character: Male, son of miner. Likes to explore caves. Went missing 2 years ago.
Prisoner: Duke of town.
Final enemy: Younger brother of duke.



# Performance

# 2024-07-17

| Method         | Mean      | Error    | StdDev   |
|--------------- |----------:|---------:|---------:|
| GenerateAll    | 756.28 ms | 5.250 ms | 4.099 ms |
| GenerateLevel1 |  37.83 ms | 0.686 ms | 0.641 ms |

# 2024-07-26

| Method         | Mean      | Error     | StdDev    |
|--------------- |----------:|----------:|----------:|
| GenerateAll    | 820.28 ms | 11.876 ms | 11.664 ms |
| GenerateLevel1 |  38.70 ms |  0.719 ms |  0.883 ms |

# 2024-07-29

| Method         | Mean      | Error    | StdDev   |
|--------------- |----------:|---------:|---------:|
| GenerateAll    | 453.49 ms | 6.863 ms | 8.924 ms |
| GenerateLevel1 |  19.48 ms | 0.284 ms | 0.252 ms |
| GenerateLevel4 |  29.48 ms | 0.584 ms | 0.573 ms |

# 2024-08-27

| Method         | Mean      | Error    | StdDev   |
|--------------- |----------:|---------:|---------:|
| GenerateAll    | 390.27 ms | 6.972 ms | 5.822 ms |
| GenerateLevel1 |  17.79 ms | 0.277 ms | 0.259 ms |
| GenerateLevel4 |  28.19 ms | 0.395 ms | 0.369 ms |
