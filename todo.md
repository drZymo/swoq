# TODO

- [ ] Order: width/height, x/y
- [ ] Levels
- [ ] Proto file hosting per level
- [ ] Single build output folder
- [ ] Sync appsettings for production/development
- [ ] Installer for ReplayViewer
- [ ] Differentiate the names of Swoq.Server.GameState and Swoq.InfraUI.Models.GameState
- [ ] Example bot Python
- [ ] Example bot C#
- [ ] Lore in the proto files
- [ ] Remember health at start of level
- [ ] Distinct player in game and player on interface. Knight & Player?
- [ ] Reduce code duplication between dashboard and replay viewer
- [ ] Autotile in map viewer

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

## Replay viewer

Show replays step by step. Allows scrolling.
Can watch folder.
Auto play mode: auto plays latest game. When finished plays next latest or loops the same again.



## Levels

1. Simple one room map. One player, no doors.
2. More difficult map with maze. One player, no doors.
3. Maze with door in front of exit. Key is placed far away from player and door.
4. One locker room. Key to exit door is placed in a room with another door. That key is in the open.
5. Two locker rooms. Repeat. 3 doors in total.
6. Double-door locker room. Two doors to enter room with exit key. Both keys are in the open. Key for inner door is close to the player at startup, so it can accidentally pick it up. Outer door key is far away from room and player.
7. First combat. Key in left side for door to enter right side. One sword and armor in left side. One enemy in right side. Open exit in right side, so evading can also be a strategy.
8. Loot combat. Same setup, but exit is locked. Enemy has key.
9. Prison. One room with a door that holds the second player. Key is somewhere on the map. Player1 must free Player2.
10. Simple two locker rooms, but now with 2 players. Correct player must pick up keys and open doors.
11. Double-door locker room with two players.
12. Single locker room for right side of map. One enemy on right side. One sword on the left, one sword on the right. No armor. So fighting together. Enemy has key for exit door.
13. Pressure plate locker room. Single locker room with a pressure plate door. One exit door, one key in locker room.
14. Double pressure plate wall. Level split in two. Pressure plate in left part for one door, plate in right part for other door. Exit in the open, so it is tempting to enter without helping other.
16. Double pressure plate wall with second pressure plate in locked room.
17. Run for sword. No more left/right sides. Enemy is in the room next to the spawn point, swords are far away on the map. Enemy has key for exit door.
18.  Double pressure plate with two lockers. First pressure plate to enter right part of map. Second plate to let second player enter right part. Then a locker room with a key for a locker on the left part of the map that contains the exit door key.
19. Two enemies. One enemy on left side, which has the key for right side. Right enemy has key for exit. One sword and armor on the left side (one player has to catch them both and attack), one sword and armor on the right side, which the other players has to get and use.
20. Two locker rooms. One with key for the other. Swords and armors in the second locker. Two enemies guarding the exit. Could accidentally follow / attack players before they have a sword.
21. Lure. One enemy (120 health) but no swords, so players cannot attack. Pressure plate room with two doors opposite to each other. Plate controls both doors. Enemy must be lured by one player into the room while other player controls the pressure plate. Leave room in time to lock the enemy. Entire room is pressure plate for exit door.


### Pressure plate locker

A locker room with the exit key. Door to the locker room can only be opened with a pressure plate. One player has to move to the plate while the other gets the key.

### Double pressure plate

Level is split in two. In left half there is a pressure plate that opens the door to the right half. Then one of the two players can enter. In the right half there is another pressure plate to open another door on the other side of the map where the other player can enter.


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


# Lore in the proto files

Add a short story in the proto file for each level about the quest.

Main character: Female, daughter of town baker. Adventurous, is rather outside than in the bakery.
Secondary character: Male, son of miner. Likes to explore caves. Went missing 2 years ago.
Prisoner: Duke of town.
Final enemy: Younger brother of duke.
