# TODO

- [ ] Act vs Move/Use
- [ ] Door open/closed states
- [ ] Map generator
- [ ] Two players
- [ ] Pressure plates
- [ ] Quest game
- [ ] Combat
- [ ] Replay storage
- [ ] Replay viewer
- [ ] Order: width/height, x/y


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

Combine the actions

## Pressure plates

When a player stands on it a door is opened. If left, the door closes.
In two player game: a red door without a key, the key is in the room behind the door. A black door with a pressure plate. One player needs to remain on plate so door is opened. Other player can then pickup key and open the door for the other player.


## Combat

- Enemies
- Sword
- Health
- Armor


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
