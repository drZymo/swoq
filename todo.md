# TODO

- [ ] Return surroundings instead of whole map
- [ ] Map generator
- [ ] Quest game
- [ ] Combat
- [ ] Replay storage
- [ ] Order: width/height, x/y

## Surroundings

Return surroundings instead of the whole map of which most is `UNKNOWN` anyway.
Size of surroundings is `VisibilityRange * 2 + 1`, so always an odd number. With range 5 the size will be 11x11. Player in in the center. In the state the current player pos is included.

It is up to the player to reconstruct the whole map with this information.


## Map generator

A generator for maps.

- Add rooms
- Connect rooms


## Combat

- Enemies
- Sword
- Health
- Armor
