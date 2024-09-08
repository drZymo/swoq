namespace Swoq.Infra;

using Swoq.Interface;
using System.Collections.Immutable;
using System.Diagnostics;
using Position = (int y, int x);

public class MapGenerator : IMapGenerator
{
    private class MapGeneratorException(string message) : Exception(message) { }
    private enum KeyColor { Red, Green, Blue }

    private const int MaxRetries = 10;
    private static readonly char[] Directions = ['N', 'E', 'S', 'W'];

    private readonly int height;
    private readonly int width;
    private readonly ImmutableHashSet<Position> allPositions = [];

    private ImmutableHashSet<Position> availablePositions = [];
    private MutableMap map;

    private Room playerRoom = new(-1, -1, -1, -1);
    private Room exitRoom = new(-1, -1, -1, -1);
    private Position exitPosition;

    private ImmutableList<Room> rooms = [];
    private ImmutableHashSet<Room> availableRooms = [];
    private ImmutableHashSet<KeyColor> availableKeyColors = [KeyColor.Red, KeyColor.Green, KeyColor.Blue];

    private ImmutableHashSet<Position> previousAvailablePositions = [];
    private ImmutableHashSet<Position> initialRestrictedPositions = [];

    public static Map Generate(int level, int height = 64, int width = 64)
    {
        try
        {
            var generator = new MapGenerator(height, width);
            return generator.Generate(level);
        }
        catch (MapGeneratorException) // TODO: report
        {
            return Map.Empty;
        }
    }

    public MapGenerator(int height, int width)
    {
        this.height = height;
        this.width = width;

        map = new(-1, height, width);
        for (var y = 0; y < this.height; y++)
        {
            for (var x = 0; x < this.width; x++)
            {
                allPositions = allPositions.Add((y, x));
            }
        }
    }

    public Map Generate(int level)
    {
        if (level < 0 || level > MaxLevel) throw new ArgumentOutOfRangeException(nameof(level));

        int tries = 0;
        while (true)
        {
            try
            {
                Reset(level);

                if (level == 0) GenerateLevel0();
                if (level == 1) GenerateLevel1();
                if (level == 2) GenerateLevel2();
                if (level == 3) GenerateLevel3();
                if (level == 4) GenerateLevel4();
                if (level == 5) GenerateLevel5();
                if (level == 6) GenerateLevel6();
                if (level == 7) GenerateLevel7();
                if (level == 8) GenerateLevel8();
                if (level == 9) GenerateLevel9();
                if (level == 10) GenerateLevel10();
                if (level == 11) GenerateLevel11();
                if (level == 12) GenerateLevel12();
                if (level == 13) GenerateLevel13();
                if (level == 14) GenerateLevel14();
                if (level == 15) GenerateLevel15();
                if (level == 16) GenerateLevel16();
                if (level == 17) GenerateLevel17();
                if (level == 18) GenerateLevel18();
                if (level == 19) GenerateLevel19();
                if (level == 20) GenerateLevel20();
                if (level == 21) GenerateLevel21();
                if (level == 22) GenerateLevel22();

                if (level == MaxLevel) map.IsFinal = true;

                // Sanity check
                foreach (var pos in availablePositions)
                {
                    Debug.Assert(map[pos] == Cell.Unknown);
                }

                AddWalls();

                if (map.Player1.Position.IsValid() && map[map.Player1.Position] != Cell.Empty) throw new MapGeneratorException("Player 1 position invalid");
                if (map.Player2.Position.IsValid() && map[map.Player2.Position] != Cell.Empty) throw new MapGeneratorException("Player 2 position invalid");

                return map.ToMap();
            }
            catch
            {
                if (tries >= MaxRetries) throw;
                tries++;
            }
        }
    }

    public int MaxLevel { get; } = MaximumLevel;

    public const int MaximumLevel = 22;

    private void Reset(int level)
    {
        // Clear map with unknowns
        map = new(level, height, width);

        // Fill with Unknown and make all positions unused
        foreach (var pos in allPositions)
        {
            map[pos] = Cell.Unknown;
        }
        availablePositions = allPositions;

        // Reset all members
        playerRoom = new(-1, -1, -1, -1);
        exitRoom = new(-1, -1, -1, -1);
        exitPosition = new();
        rooms = [];
        availableRooms = [];
        availableKeyColors = [KeyColor.Red, KeyColor.Green, KeyColor.Blue];
        previousAvailablePositions = [];
        initialRestrictedPositions = [];
    }

    private void AddWalls()
    {
        bool IsUnknown(Position pos)
        {
            return pos.y < 0 || pos.y >= height ||
                pos.x < 0 || pos.x >= width ||
                map[pos] == Cell.Unknown;
        }

        ImmutableList<Position> walls = [];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (map[y, x] == Cell.Unknown)
                {
                    if (!IsUnknown((y - 1, x - 1)) || !IsUnknown((y - 1, x)) || !IsUnknown((y - 1, x + 1)) ||
                        !IsUnknown((y, x - 1)) || !IsUnknown((y, x)) || !IsUnknown((y, x + 1)) ||
                        !IsUnknown((y + 1, x - 1)) || !IsUnknown((y + 1, x)) || !IsUnknown((y + 1, x + 1)))
                    {
                        walls = walls.Add((y, x));
                    }
                }
            }
        }

        foreach (var pos in walls)
        {
            map[pos] = Cell.Wall;
        }
    }

    private void GenerateLevel0()
    {
        /// Simple 1 room

        var room = CreateRoom(height / 2, width / 2, 10, 10);
        map.Player1.Position = (room.Top, room.Left);
        map[room.Bottom - 1, room.Right - 1] = Cell.Exit;
    }

    private void GenerateLevel1()
    {
        /// Just a standard maze, no doors

        CreateStandardMaze();
    }

    private void GenerateLevel2()
    {
        /// Door around exit

        // One door around exit. Key in room far away from start and exit
        CreateStandardMaze();

        // Add exit door
        var (keyColor, doorPos) = AddLockAroundExit();

        // Place key in far away room
        var keyPosition = ClaimRandomPositionInAvailableRoomFarthestFrom([map.Player1.Position, doorPos]);
        map[keyPosition] = ToKey(keyColor);
    }

    private void GenerateLevel3()
    {
        /// One locker room

        CreateStandardMaze();

        // Add exit door
        var (exitKeyColor, exitDoorPos) = AddLockAroundExit();

        // Place locker in room farthest away from player and exit large enough for locker room.
        var lockerPos = ClaimRandomPositionInAvailableRoomFarthestFrom([map.Player1.Position, exitDoorPos], margin: 2, minRoomHeight: 5, minRoomWidth: 5);
        var (lockerKeyColor, infrontLockerDoorPos) = AddLocker(lockerPos, exitKeyColor);

        // Place key farthest away from player and locker room
        var lockerKeyPos = ClaimRandomPositionInAvailableRoomFarthestFrom([map.Player1.Position, infrontLockerDoorPos]);
        map[lockerKeyPos] = ToKey(lockerKeyColor);
    }

    private void GenerateLevel4()
    {
        /// Two locker rooms

        CreateStandardMaze();

        // Add exit door
        var (exitKeyColor, exitDoorPos) = AddLockAroundExit();

        // Place locker in room farthest away from player and exit large enough for locker room.
        var locker1Pos = ClaimRandomPositionInAvailableRoomFarthestFrom([map.Player1.Position, exitDoorPos], margin: 2, minRoomHeight: 5, minRoomWidth: 5);
        var (locker1KeyColor, infrontLocker1DoorPos) = AddLocker(locker1Pos, exitKeyColor);

        // Add another locker
        var locker2Pos = ClaimRandomPositionInAvailableRoomFarthestFrom([map.Player1.Position, infrontLocker1DoorPos], margin: 2, minRoomHeight: 5, minRoomWidth: 5);
        var (locker2KeyColor, infrontLocker2DoorPos) = AddLocker(locker2Pos, locker1KeyColor);

        // Place key farthest away from player and locker room
        var locker2KeyPos = ClaimRandomPositionInAvailableRoomFarthestFrom([map.Player1.Position, infrontLocker1DoorPos, infrontLocker2DoorPos]);
        map[locker2KeyPos] = ToKey(locker2KeyColor);
    }

    private void GenerateLevel5()
    {
        /// Double-door locker room.
        /// Two doors to enter room with exit key.
        /// Both keys are in the open.
        /// Key for inner door is close to the player at startup, so it can accidentally pick it up.
        /// Outer door key is far away from room and player.

        var (innerKeyPos, innerColor, outerKeyPos, outerColor) = CreateDoubleLockerRoomMaze();

        map[innerKeyPos] = ToKey(innerColor);
        map[outerKeyPos] = ToKey(outerColor);
    }

    private void GenerateLevel6()
    {
        /// Exit with boulders around

        CreateStandardMaze();

        // Place wall of boulders around exit
        SetIfEmpty(exitPosition.y - 1, exitPosition.x - 1, Cell.Wall);
        SetIfEmpty(exitPosition.y - 1, exitPosition.x + 1, Cell.Wall);
        SetIfEmpty(exitPosition.y + 1, exitPosition.x - 1, Cell.Wall);
        SetIfEmpty(exitPosition.y + 1, exitPosition.x + 1, Cell.Wall);
        SetIfEmpty(exitPosition.y - 1, exitPosition.x, Cell.Boulder);
        SetIfEmpty(exitPosition.y, exitPosition.x - 1, Cell.Boulder);
        SetIfEmpty(exitPosition.y, exitPosition.x + 1, Cell.Boulder);
        SetIfEmpty(exitPosition.y + 1, exitPosition.x, Cell.Boulder);
    }

    private void GenerateLevel7()
    {
        /// Exit with pressure plate door

        // Fixed room position and size for the exit
        CreateRoom(height - 5, width - 5, 8, 8);
        // Fill with standard maze
        CreateStandardMaze();

        var keyColor = PickRandomAvailableKeyColor();

        // Place wall of doors around exit
        ImmutableList<Position> doorPositions = [];
        for (var y = exitPosition.y - 1; y <= exitPosition.y + 1; y++)
        {
            for (var x = exitPosition.x - 1; x <= exitPosition.x + 1; x++)
            {
                if (0 <= y && y < height && 0 <= x && x < width)
                {
                    if (SetIfEmpty(y, x, ToDoor(keyColor)))
                    {
                        doorPositions = doorPositions.Add((y, x));
                    }
                }
            }
        }

        // Find the minimum walk distance from each empty point on the map to one of the doors.
        var points = ImmutableDictionary<Position, int>.Empty;
        foreach (var doorPos in doorPositions)
        {
            var (distances, paths) = ComputeDistancesFrom(doorPos);
            foreach (var (pt, dist) in distances)
            {
                var minDist = dist;
                if (points.TryGetValue(pt, out var oldDist))
                {
                    minDist = Math.Min(minDist, oldDist);
                }
                points = points.SetItem(pt, minDist);
            }
        }

        // Place a plate on a point at a fixed walk distance from the exit.
        const int plateDistance = 5;

        var platePos = points.Where(kvp => kvp.Value == plateDistance).Select(kvp => kvp.Key).PickOne();
        map[platePos] = ToPressurePlate(keyColor);

        // Place several boulders around map
        for (var i = 0; i < 6; i++)
        {
            var boulderPos = ClaimRandomPositionInRandomAvailableRoom(availableRooms, margin: 1);
            map[boulderPos] = Cell.Boulder;
        }
    }

    private void GenerateLevel8()
    {
        /// First enemy. Run to exit.
        /// In room of exit. 
        /// No sword.
        CreateStandardMaze();

        var enemyPos = GetRandomEmptyPositionInRoom(exitRoom, margin: 1);
        map.Enemy1.Position = enemyPos;

        // Place several boulders around map
        for (var i = 0; i < 6; i++)
        {
            var boulderPos = ClaimRandomPositionInRandomAvailableRoom(availableRooms, margin: 1);
            map[boulderPos] = Cell.Boulder;
        }
    }

    private void GenerateLevel9()
    {
        /// Lure. 
        /// One enemy with key to exit door. 
        /// In a room before exit.
        /// Room is locked with second door with pressure plate in front.
        /// Crush enemy with door to get key.
        /// No swords.

        // Create a maze with an additional exit room below it
        RestrictAvailablePositions(maxY: height - 6);
        CreateRandomRooms(maxRooms: 200, minSize: 1, maxSize: 7, margin: 1);
        ConnectRoomsRandomly();
        RestoreAvailablePositions();
        exitRoom = CreateRoomTopLeft(height - 6, width - 8, 5, 7);

        // Find room closest to left of exit room and connect them
        // This will make sure it always enters from the left.
        var connectRoom = rooms.Where(r => r.Right < exitRoom.Left).OrderBy(r => exitRoom.Center.DistanceTo(r.Center)).First();
        ConnectRooms(exitRoom, connectRoom);

        // Add player and exit with door around
        PlacePlayersTopLeftAndExitBottomRight(false);
        var (exitKeyColor, _) = AddLockAroundExit();

        // Create door for entering exit room with same color as exit
        var entryDoorCell = ToDoor(exitKeyColor);
        ImmutableList<(Position enemyPos, Position platePos)> enemyAndPlatePositions = [];
        for (var y = exitRoom.Top; y <= exitRoom.Bottom; y++)
        {
            if (SetIfEmpty(y, exitRoom.Left - 1, entryDoorCell))
            {
                var enemyPos = (y, exitRoom.Left + 1);
                var platePos = (y, exitRoom.Left - 3);
                if (IsEmpty(enemyPos) && IsEmpty(platePos))
                {
                    enemyAndPlatePositions = enemyAndPlatePositions.Add((enemyPos, platePos));
                }
            }
        }

        // Place enemy right of door
        var (enemyPosition, platePosition) = enemyAndPlatePositions.PickOne();
        map.Enemy1.Position = enemyPosition;
        map.Enemy1.Inventory = ToInventory(exitKeyColor);
        // and plate left of door
        map[platePosition] = ToPressurePlate(exitKeyColor);
    }

    private void GenerateLevel10()
    {
        /// First combat.
        /// Locked exit. One enemy with key to exit door
        /// One sword and health in initial room.

        var playerRoom = CreateRoom(4, 4, 7, 7);
        CreateStandardMaze();
        var (exitKeyColor, exitDoorPos) = AddLockAroundExit();

        // Place sword and health in initial room
        map[playerRoom.Top, 4] = Cell.Sword;
        map[4, playerRoom.Left] = Cell.Health;

        // Place enemy with key to exit in a room far away
        var enemyPos = ClaimRandomPositionInAvailableRoomFarthestFrom([map.Player1.Position, exitDoorPos]);
        map.Enemy1.Position = enemyPos;
        map.Enemy1.Inventory = ToInventory(exitKeyColor);
    }

    private void GenerateLevel11()
    {
        /// Two enemies.
        /// First enemy drops key for room with second enemy.
        /// Second enemy has key for exit.
        /// 3 health needed to win from two enemies. Player health = 5+3*3 = 14, Enemy health = 12

        // maze with locked exit
        CreateStandardMaze();
        var (exitKeyColor, exitDoorPos) = AddLockAroundExit();

        // Put enemy with key to exit in a room
        var chamberPos = ClaimRandomPositionInAvailableRoomFarthestFrom([exitDoorPos, map.Player1.Position], margin: 3, minRoomHeight: 9, minRoomWidth: 9);
        var chamberKeyColor = CreateChamber(chamberPos, 5, 5);
        map.Enemy1.Position = chamberPos;
        map.Enemy1.Inventory = ToInventory(exitKeyColor);

        // Add enemy with key to room near exit
        var enemy2Room = ClaimClosestAvailableRoomFrom(exitDoorPos);
        var enemy2Pos = GetRandomEmptyPositionInRoom(enemy2Room);
        map.Enemy2.Position = enemy2Pos;
        map.Enemy2.Inventory = ToInventory(chamberKeyColor);

        // Place sword on a random position
        var swordPos = ClaimRandomPositionInRandomAvailableRoom(availableRooms);
        map[swordPos] = Cell.Sword;

        // Place 3 health on random positions
        for (var i = 0; i < 3; i++)
        {
            var healthPos = ClaimRandomPositionInRandomAvailableRoom(availableRooms);
            map[healthPos] = Cell.Health;
        }
    }

    private void GenerateLevel12()
    {
        /// Prison
        /// One big room that holds the second player.
        /// Guard has key to prison.
        /// Exit is open.
        /// Health and sword spread around map.

        // Need at least one big room that can fit prison
        RestrictAvailablePositions(5, height - 5, 5, width - 5);
        var prisonRoom = CreateRandomRoom(5, 7, 0);
        RestoreAvailablePositions();

        Debug.Assert(prisonRoom != null);
        // Fill rest with standard maze
        CreateStandardMaze();

        availableRooms = availableRooms.Remove(prisonRoom);

        // Create prison room
        var prisonKeyColor = PickRandomAvailableKeyColor();

        var (cy, cx) = prisonRoom.Center;
        for (int x = cx - 3; x <= cx + 3; x++)
        {
            SetIfEmpty(cy - 3, x, Cell.Wall);
            SetIfEmpty(cy + 3, x, Cell.Wall);
        }
        for (int y = cy - 2; y <= cy + 2; y++)
        {
            SetIfEmpty(y, cx - 3, Cell.Wall);
            SetIfEmpty(y, cx + 3, Cell.Wall);
        }

        // Player 2 is in prison
        map.Player2.Position = prisonRoom.Center;

        // Add large door with enemy guard in front
        var doorCell = ToDoor(prisonKeyColor);
        int direction = Directions.PickOne();
        switch (direction)
        {
            case 'N':
                map[cy - 3, cx - 1] = doorCell;
                map[cy - 3, cx] = doorCell;
                map[cy - 3, cx + 1] = doorCell;
                map.Enemy1.Position = (cy - 4, cx);
                break;
            case 'E':
                map[cy - 1, cx + 3] = doorCell;
                map[cy, cx + 3] = doorCell;
                map[cy + 1, cx + 3] = doorCell;
                map.Enemy1.Position = (cy, cx + 4);
                break;
            case 'S':
                map[cy + 3, cx - 1] = doorCell;
                map[cy + 3, cx] = doorCell;
                map[cy + 3, cx + 1] = doorCell;
                map.Enemy1.Position = (cy + 4, cx);
                break;
            case 'W':
                map[cy - 1, cx - 3] = doorCell;
                map[cy, cx - 3] = doorCell;
                map[cy + 1, cx - 3] = doorCell;
                map.Enemy1.Position = (cy, cx - 4);
                break;

            default: throw new InvalidOperationException();
        }

        // Give guard the key
        map.Enemy1.Inventory = ToInventory(prisonKeyColor);

        // Place a sword randomly
        var swordPos = ClaimRandomPositionInAvailableRoomFarthestFrom([map.Player1.Position, map.Enemy1.Position]);
        map[swordPos] = Cell.Sword;

        // Place health randomly
        var healthPos = ClaimRandomPositionInAvailableRoomFarthestFrom([map.Player1.Position, map.Enemy1.Position, swordPos]);
        map[healthPos] = Cell.Health;
    }

    private void GenerateLevel13()
    {
        /// Simple two locker rooms, but now with 2 players.
        /// Correct player must pick up keys and open doors.

        CreateStandardMaze(twoPlayers: true);

        // Add exit door
        var (exitKeyColor, exitDoorPos) = AddLockAroundExit();

        // Place locker in room farthest away from player and exit large enough for locker room.
        var locker1Pos = ClaimRandomPositionInAvailableRoomFarthestFrom([map.Player1.Position, exitDoorPos], margin: 2, minRoomHeight: 5, minRoomWidth: 5);
        var (locker1KeyColor, infrontLocker1DoorPos) = AddLocker(locker1Pos, exitKeyColor);

        // Add another locker
        var locker2Pos = ClaimRandomPositionInAvailableRoomFarthestFrom([map.Player1.Position, infrontLocker1DoorPos], margin: 2, minRoomHeight: 5, minRoomWidth: 5);
        var (locker2KeyColor, infrontLocker2DoorPos) = AddLocker(locker2Pos, locker1KeyColor);

        // Place key farthest away from player and locker room
        var locker2KeyPos = ClaimRandomPositionInAvailableRoomFarthestFrom([map.Player1.Position, infrontLocker1DoorPos, infrontLocker2DoorPos]);
        map[locker2KeyPos] = ToKey(locker2KeyColor);
    }

    private void GenerateLevel14()
    {
        /// Double-door locker room.
        /// With two players.
        /// Again correct key must be picked up first.

        var (innerKeyPos, innerColor, outerKeyPos, outerColor) = CreateDoubleLockerRoomMaze(twoPlayers: true);

        map[innerKeyPos] = ToKey(innerColor);
        map[outerKeyPos] = ToKey(outerColor);
    }

    private void GenerateLevel15()
    {
        /// Pressure plate wall.
        /// Two sided level with two corridors.
        /// One locked with pressure plate door, other with regular door.
        /// Pressure plate on left side, key to door on the right side.
        /// Must work together to open regular door.
        /// Exit in the open, so it is tempting to enter without helping other.

        var (middle, roomsLeft, roomsRight) = CreateSplitMaze(twoPlayers: true);

        var (exitKeyColor, _) = AddLockAroundExit();

        // Create a tunnel with a door
        var firstTunnelKeyColor = PickRandomAvailableKeyColor();
        var firstTunnelDoorY = ConnectLeftAndRightWithDoor(middle, roomsLeft, roomsRight, firstTunnelKeyColor);
        var infrontFirstTunnelDoor = (firstTunnelDoorY, middle - 1); // left side

        // Place a pressure plate in the left (only rooms in left can reach position in front of door)
        var platePos = ClaimRandomPositionInAvailableRoomFarthestFrom([map.Player1.Position, infrontFirstTunnelDoor]);
        map[platePos] = ToPressurePlate(firstTunnelKeyColor);

        // Create another tunnel with a random door
        var secondTunnelKeyColor = PickRandomAvailableKeyColor();
        var secondTunnelDoorY = ConnectLeftAndRightWithDoor(middle, roomsLeft, roomsRight, secondTunnelKeyColor);
        var infrontSecondTunnelDoor = (secondTunnelDoorY, middle + 1); // right side

        // Place key in right (only rooms in right can reach position in front of door).
        var keyPos = ClaimRandomPositionInAvailableRoomFarthestFrom([infrontSecondTunnelDoor]);
        map[keyPos] = ToKey(secondTunnelKeyColor);

        // Put exit key on room in the left close to the start
        var exitKeyRoom = ClaimClosestAvailableRoomFrom(map.Player1.Position);
        var exitKeyPos = GetRandomEmptyPositionInRoom(exitKeyRoom);
        map[exitKeyPos] = ToKey(exitKeyColor);
    }

    private void GenerateLevel16()
    {
        /// Double pressure plate.
        /// Double-door locker room.
        /// Pressure plate for both doors.
        /// One boulder in the level.
        /// Key for exit door in locker.
        /// One player needs to stand on pressure plate, other needs a boulder on it.

        var (innerKeyPos, innerColor, outerKeyPos, outerColor) = CreateDoubleLockerRoomMaze(true);

        // Make pressure plates to trigger doors
        map[innerKeyPos] = ToPressurePlate(innerColor);
        map[outerKeyPos] = ToPressurePlate(outerColor);

        // Place one boulder
        var boulderPos = ClaimRandomPositionInRandomAvailableRoom(availableRooms, margin: 1);
        map[boulderPos] = Cell.Boulder;
    }

    private void GenerateLevel17()
    {
        /// Two sided maze with door.
        /// Left side has swords, no health.
        /// Right side has one enemy with key to exit.
        /// Work together to kill enemy.

        var (middle, roomsLeft, roomsRight) = CreateSplitMaze(twoPlayers: true);

        var (exitKeyColor, _) = AddLockAroundExit();

        // Create door in tunnel between left and right
        var keyColor = PickRandomAvailableKeyColor();
        var doorPosY = ConnectLeftAndRightWithDoor(middle, roomsLeft, roomsRight, keyColor);
        var infrontOfDoor = (doorPosY, middle - 1);

        var keyPos = ClaimRandomPositionInAvailableRoomFarthestFrom([map.Player1.Position, infrontOfDoor]);
        map[keyPos] = ToKey(keyColor);

        // Place sword in any room on the left
        var swordPos1 = ClaimRandomPositionInRandomAvailableRoom(roomsLeft);
        map[swordPos1] = Cell.Sword;

        // Place another sword in any room on the left
        var swordPos2 = ClaimRandomPositionInRandomAvailableRoom(roomsLeft);
        map[swordPos2] = Cell.Sword;

        // Place enemy in any room on the right with key to exit
        var enemyPos = ClaimRandomPositionInRandomAvailableRoom(roomsRight);
        map.Enemy1.Position = enemyPos;
        map.Enemy1.Inventory = ToInventory(exitKeyColor);
    }

    private void GenerateLevel18()
    {
        /// Run for sword.
        /// No more left/right sides.
        /// Enemy is in the room next to the spawn point, swords are far away on the map.
        /// Enemy has key for exit door.

        CreateStandardMaze(twoPlayers: true);

        var (exitKeyColor, _) = AddLockAroundExit();

        var enemyRoom = ClaimClosestAvailableRoomFrom(map.Player1.Position);
        var enemyPos = GetRandomEmptyPositionInRoom(enemyRoom, margin: 1);
        map.Enemy1.Position = enemyPos;
        map.Enemy1.Inventory = ToInventory(exitKeyColor);

        var sword1Pos = ClaimRandomPositionInAvailableRoomFarthestFrom([map.Player1.Position, enemyPos]);
        map[sword1Pos] = Cell.Sword;
        var sword2Pos = ClaimRandomPositionInAvailableRoomFarthestFrom([map.Player2.Position, enemyPos, sword1Pos]);
        map[sword2Pos] = Cell.Sword;
    }

    private void GenerateLevel19()
    {
        /// Two enemies.
        /// Split maze. One enemy on left side in front of tunnel door. Other in front of exit door. 
        /// One sword and health in left side. Extra heath in right side. 
        /// First enemy drops sword for second player. Second enemy drops key for exit door.
        /// Total player health = 5+3 + 5+3 = 16. Total enemy health = 12. One player total health = 5+3+3 = 11, so need two player interaction.
        /// One player needs to collect sword and health first then kill enemy so second player can grab the sword.

        var (middle, roomsLeft, roomsRight) = CreateSplitMaze(twoPlayers: true);
        var tunnelDoorColor = PickRandomAvailableKeyColor();
        var tunnelDoorPosition = ConnectLeftAndRightWithDoor(middle, roomsLeft, roomsRight, tunnelDoorColor);
        var (exitKeyColor, _) = AddLockAroundExit();

        // One enemy at the door in the tunnel
        map.Enemy1.Position = (tunnelDoorPosition, middle - 1);
        map.Enemy1.Inventory = ToInventory(tunnelDoorColor);

        // Other enemy near exit
        var enemy2Pos = GetRandomEmptyPositionInRoom(exitRoom);
        map.Enemy2.Position = enemy2Pos;
        map.Enemy2.Inventory = ToInventory(exitKeyColor);

        // One health and sword in left side
        var health1Pos = ClaimRandomPositionInRoomFarthestFrom(roomsLeft, [map.Player1.Position, map.Player2.Position, map.Enemy1.Position]);
        map[health1Pos] = Cell.Health;
        var sword1Pos = ClaimRandomPositionInRoomFarthestFrom(roomsLeft, [map.Player1.Position, map.Player2.Position, map.Enemy1.Position]);
        map[sword1Pos] = Cell.Sword;

        // One health on right side
        var health2Pos = ClaimRandomPositionInRoomFarthestFrom(roomsRight, [(tunnelDoorPosition, middle + 1), map.Enemy2.Position]);
        map[health2Pos] = Cell.Health;
    }

    private void GenerateLevel20()
    {
        /// Separation
        /// At start, pressure plate to open one door. Player1 needs to step on it so Player2 can enter next room.
        /// In that room another pressure plate to open other door in start room. Player2 needs to step on it so Player1 can enter other room.
        /// Now both players are in a separate part of the map, where the both have to kill their own enemy.
        /// Each enemy leaves a key to enter the final part of the map where both players are joined again.
        /// One final enemy with key to exit.

        // Create dedicated player and double exit rooms
        var playerRoomWidth = 7; // excluding walls
        var upperHeight = height / 2;

        var playerRoomHeight = Math.Min(9, upperHeight - 2); // excluding walls
        var playerRoom = CreateRoomTopLeft(1, 1, playerRoomHeight, playerRoomWidth);

        var exitRoomWidth = 8; // excluding walls
        var exitRoomHeight = exitRoomWidth;
        var preExitRoomWidth = exitRoomWidth;
        var preExitRoomHeight = 5; // excluding walls

        var preExitRoomLeft = (width - 1) - preExitRoomWidth;
        var preExitRoomTop = upperHeight + 1;
        var preExitRoom = CreateRoomTopLeft(preExitRoomTop, preExitRoomLeft, preExitRoomHeight, preExitRoomWidth);

        var exitRoomLeft = (width - 1) - exitRoomWidth;
        var exitRoomTop = preExitRoom.Bottom + 1;
        var exitRoom = CreateRoomTopLeft(exitRoomTop, exitRoomLeft, exitRoomHeight, exitRoomWidth);

        var initialRooms = rooms;

        // Upper part with entry and exit rooms
        rooms = [];
        Room upperEntryRoom, upperExitRoom;
        {
            var minX = playerRoomWidth + 2;
            upperEntryRoom = CreateRoomTopLeft(1, minX, 5, 5);
            upperExitRoom = CreateRoomTopLeft(upperHeight - 5, width - 1 - 5, 5, 5);
            RestrictAvailablePositions(minX: minX, maxY: upperHeight);
            CreateRandomRooms(maxRooms: 200, minSize: 1, maxSize: 5, margin: 1);
            ConnectRoomsRandomly();
            RestoreAvailablePositions();
        }
        var roomsUpper = rooms;

        // Lower part with entry and exit rooms
        rooms = [];
        Room lowerEntryRoom, lowerExitRoom;
        {
            var maxX = preExitRoom.Left - 1;
            lowerEntryRoom = CreateRoomTopLeft(upperHeight + 1, 1, 5, 5);
            lowerExitRoom = CreateRoomTopLeft(upperHeight + 1, maxX - 5, 5, 5);
            RestrictAvailablePositions(maxX: maxX, minY: upperHeight);
            CreateRandomRooms(maxRooms: 200, minSize: 1, maxSize: 5, margin: 1);
            ConnectRoomsRandomly();
            RestoreAvailablePositions();
        }
        var roomsLower = rooms;

        rooms = initialRooms.AddRange(roomsUpper).AddRange(roomsLower);

        // Connect rooms
        ConnectRooms(playerRoom, upperEntryRoom);
        ConnectRooms(playerRoom, lowerEntryRoom);
        ConnectRooms(upperExitRoom, preExitRoom);
        ConnectRooms(lowerExitRoom, preExitRoom);
        ConnectRooms(preExitRoom, exitRoom);
        availableRooms = availableRooms.Remove(upperEntryRoom).Remove(upperExitRoom);
        availableRooms = availableRooms.Remove(lowerEntryRoom).Remove(lowerExitRoom);

        // Add doors to entry and exit rooms
        var upperDoorColor = PickRandomAvailableKeyColor();
        var upperDoorCell = ToDoor(upperDoorColor);
        for (var y = playerRoom.Top; y <= playerRoom.Bottom; y++)
        {
            SetIfEmpty(y, playerRoom.Right, upperDoorCell);
        }
        for (var x = preExitRoom.Left; x <= preExitRoom.Right; x++)
        {
            SetIfEmpty(preExitRoom.Top - 1, x, upperDoorCell);
        }

        var lowerDoorColor = PickRandomAvailableKeyColor();
        var lowerDoorCell = ToDoor(lowerDoorColor);
        for (var x = playerRoom.Left; x <= playerRoom.Right; x++)
        {
            SetIfEmpty(playerRoom.Bottom, x, lowerDoorCell);
        }
        for (var y = preExitRoom.Top; y <= preExitRoom.Bottom; y++)
        {
            SetIfEmpty(y, preExitRoom.Left - 1, lowerDoorCell);
        }

        // Players in player room
        availableRooms = availableRooms.Remove(playerRoom);
        map.Player1.Position = (1, 3);
        map.Player2.Position = (3, 1);

        // Exit in exit room
        availableRooms = availableRooms.Remove(exitRoom);
        exitPosition = (exitRoom.Bottom - 1, exitRoom.Right - 1);
        map[exitPosition] = Cell.Exit;
        var (exitDoorColor, _) = AddLockAroundExit();

        // Add pressure plates for entering parts
        var upperPlatePos = GetRandomEmptyPositionInRoom(playerRoom);
        map[upperPlatePos] = ToPressurePlate(upperDoorColor);

        var lowerPlatePos = GetRandomEmptyPositionInRoom(upperEntryRoom);
        map[lowerPlatePos] = ToPressurePlate(lowerDoorColor);

        // Enemies with keys
        map.Enemy1.Position = GetRandomEmptyPositionInRoom(upperExitRoom, margin: 1);
        map.Enemy1.Inventory = ToInventory(upperDoorColor);

        map.Enemy2.Position = GetRandomEmptyPositionInRoom(lowerExitRoom, margin: 1);
        map.Enemy2.Inventory = ToInventory(lowerDoorColor);

        map.Enemy3.Position = GetRandomEmptyPositionInRoom(exitRoom, margin: 1);
        map.Enemy3.Inventory = ToInventory(exitDoorColor);

        // One health and swords in each part
        map[ClaimRandomPositionInRandomAvailableRoom(roomsLower)] = Cell.Sword;
        map[ClaimRandomPositionInRandomAvailableRoom(roomsLower)] = Cell.Health;

        map[ClaimRandomPositionInRandomAvailableRoom(roomsUpper)] = Cell.Sword;
        map[ClaimRandomPositionInRandomAvailableRoom(roomsUpper)] = Cell.Health;

        // Extra health in pre-exit room
        map[GetRandomEmptyPositionInRoom(preExitRoom)] = Cell.Health;
        map[GetRandomEmptyPositionInRoom(preExitRoom)] = Cell.Health;
    }

    private void GenerateLevel21()
    {
        /// Grand desert.
        /// Double pressure plate locker room with two swords and two health.
        /// Players have to take turns getting swords and health.
        /// 2x health and boulder scattered around map.
        /// Pre-exit room with two guards.
        /// One extra guard in exit room with key for exit.

        // Create double exit room to hold three guards
        exitRoom = CreateRoom(height - 4, width - 4, 6, 6);
        var preExitRoom = CreateRoom(height - 13, width - 6, 10, 10);
        SetAndMakeUnavailable((height - 8, width - 2), Cell.Empty);
        SetAndMakeUnavailable((height - 8, width - 3), Cell.Empty);

        // Need at least one big room where double locker can fit.
        // Locker room is 5x5, so need at least 9x9 room to have space around
        RestrictAvailablePositions(5, height - 7, 5, width - 15);
        var lockerRoom = CreateRandomRoom(4, 7, 1);
        Debug.Assert(lockerRoom != null);
        RestoreAvailablePositions();

        // Prevent connecting exit room
        rooms = rooms.Remove(exitRoom);

        // Fill the rest with standard maze
        RestrictAvailablePositions(maxY: height - 7);
        CreateStandardMaze(twoPlayers: true);
        RestoreAvailablePositions();

        availableRooms = availableRooms.Remove(exitRoom).Remove(preExitRoom);

        var (exitKeyColor, exitDoor) = AddLockAroundExit();

        // Big locker room

        // Make sure locker rooms is claimed
        availableRooms = availableRooms.Remove(lockerRoom);

        var (cy, cx) = lockerRoom.Center;

        // Swords in inner room, blocked by boulder
        map[cy - 1, cx - 1] = Cell.Sword;
        map[cy - 1, cx + 1] = Cell.Sword;

        // Walls of inner room
        for (int x = cx - 2; x <= cx + 2; x++)
        {
            SetIfEmpty(cy - 2, x, Cell.Wall);
            SetIfEmpty(cy, x, Cell.Wall);
            SetIfEmpty(cy + 1, x, Cell.Wall);
        }
        SetIfEmpty(cy - 1, cx - 2, Cell.Wall);
        SetIfEmpty(cy - 1, cx + 2, Cell.Wall);

        // Doors of inner and outer rooms
        var innerKeyColor = PickRandomAvailableKeyColor();
        map[cy, cx] = ToDoor(innerKeyColor);

        var outerKeyColor = PickRandomAvailableKeyColor();
        map[cy + 1, cx] = ToDoor(outerKeyColor);

        // Block outer door with boulder
        map[cy + 2, cx] = Cell.Boulder;


        // Place inner key in room closest to player so it can accidentally be picked up
        var innerKeyRoom = ClaimClosestAvailableRoomFrom(map.Player1.Position);
        var innerKeyPos = GetRandomEmptyPositionInRoom(innerKeyRoom, 1);

        // Place outer key far from exit and player
        var outerKeyPos = ClaimRandomPositionInAvailableRoomFarthestFrom([map.Player1.Position, exitDoor]);

        // Make pressure plates to trigger doors
        map[innerKeyPos] = ToPressurePlate(innerKeyColor);
        map[outerKeyPos] = ToPressurePlate(outerKeyColor);

        // 4 random health
        // Players now have 2*5 + 4*3 = 22 health.
        // Three enemies have 3*6 = 18 health
        for (var h = 0; h < 4; h++)
        {
            var healthPos = ClaimRandomPositionInRandomAvailableRoom(availableRooms);
            map[healthPos] = Cell.Health;
        }

        // Three guards in exit rooms
        var enemy1Pos = GetRandomEmptyPositionInRoom(preExitRoom, margin: 1);
        map.Enemy1.Position = enemy1Pos;

        var enemy2Pos = GetRandomEmptyPositionInRoom(preExitRoom, margin: 1);
        map.Enemy2.Position = enemy2Pos;

        map.Enemy3.Position = (height - 6, width - 7);
        map.Enemy3.Inventory = ToInventory(exitKeyColor);
    }

    private void GenerateLevel22()
    {
        /// Crush.
        /// One enemy (with lots of health and damage),
        /// swords and health in level, but still not enough to defeat boss.
        /// Corridor/room with pressure plate controlled door wall.
        /// One player must lure the boss on the plate.
        /// Other player must stand on pressure plate (somewhere far away) and step off when boss is on the door position.
        /// Door is closed and kills boss.
        /// Boss loot is key for exit door and two big treasures and are placed next to closed door.
        /// Without treasure in inventory player is killed when leaving.
        /// Lots of health, swords, boulders and random keys arround map.

        // Big room for boss near exit

        // Create exit room
        exitRoom = CreateRoomTopLeft(height - 1 - 5, width - 1 - 5, 5, 5, margin: 1);

        // Create a random large room in the right part of the map for the boss
        RestrictAvailablePositions(minX: width / 2);
        var bossRoom = CreateRandomRoom(minSize: 5, maxSize: 5, margin: 1)
            ?? throw new MapGeneratorException("Random room did not fit");
        RestoreAvailablePositions();

        // Fill up the rest with a standard maze
        CreateStandardMaze(twoPlayers: true, maxSize: 4);

        var (exitKeyColor, _) = AddLockAroundExit();

        // Place boss in room
        map.Enemy1.Position = bossRoom.Center;
        map.Enemy1.IsBoss = true;
        map.Enemy1.Inventory = ToInventory(exitKeyColor);
        availableRooms = availableRooms.Remove(bossRoom);

        // First find or create tunnel in left side of room
        var tunnelPosX = bossRoom.Left - 1;
        var tunnelPosY = FindHorizontalTunnel(bossRoom.Top, bossRoom.Bottom, tunnelPosX);
        if (!tunnelPosY.HasValue)
        {
            var bossRoomLeft = rooms.Where(r => r.Right < tunnelPosX).OrderBy(r => r.Center.DistanceTo(bossRoom.Center)).First();
            ConnectRooms(bossRoom, bossRoomLeft);
            tunnelPosY = FindHorizontalTunnel(bossRoom.Top, bossRoom.Bottom, tunnelPosX);
        }

        // Reject no pos found situation
        if (!tunnelPosY.HasValue)
        {
            throw new MapGeneratorException("No tunnel to boss room");
        }

        // Place a door in the tunnel
        Position tunnelDoorPos = (tunnelPosY.Value, tunnelPosX);
        var tunnelDoorColor = PickRandomAvailableKeyColor();
        var tunnelDoorCell = ToDoor(tunnelDoorColor);
        SetIfEmpty(tunnelDoorPos.y, tunnelDoorPos.x, tunnelDoorCell);
        SetIfEmpty(tunnelDoorPos.y + 1, tunnelDoorPos.x, tunnelDoorCell);

        // Pressure plate in a room close by
        var plateRoom = ClaimClosestAvailableRoomFrom(tunnelDoorPos);
        var platePos = GetRandomEmptyPositionInRoom(plateRoom, margin: 1);
        map[platePos] = ToPressurePlate(tunnelDoorColor);

        // Random swords, health, and boulders
        // Leave margin of 1 so always a path around is possible.
        for (var i = 0; i < 5; i++) PlaceRandomly(Cell.Sword);
        for (var i = 0; i < 20; i++) PlaceRandomly(Cell.Health);
        for (var i = 0; i < 5; i++) PlaceRandomly(Cell.Boulder);

        // Place a the last key somewhere, just for confusion, not needed.
        var unusedKeyColor = PickRandomAvailableKeyColor();
        PlaceRandomly(ToKey(unusedKeyColor));
    }

    private Room CreateRoomTopLeft(int top, int left, int height, int width, int margin = 0)
        => CreateRoom(top + height / 2, left + width / 2, height, width, margin);

    private Room CreateRoom(int y, int x, int height, int width, int margin = 0)
    {
        var room = new Room(y, x, height, width);

        // Make empty
        for (var my = room.Top; my < room.Bottom; my++)
        {
            for (var mx = room.Left; mx < room.Right; mx++)
            {
                map[my, mx] = Cell.Empty;
            }
        }

        // Make room locations unavailable including margin
        var top = Math.Max(0, room.Top - margin);
        var bottom = Math.Min(room.Bottom + margin, this.height);
        var left = Math.Max(0, room.Left - margin);
        var right = Math.Min(room.Right + margin, this.width);
        for (var my = top; my < bottom; my++)
        {
            for (var mx = left; mx < right; mx++)
            {
                availablePositions = availablePositions.Remove((my, mx));
            }
        }

        rooms = rooms.Add(room);
        availableRooms = availableRooms.Add(room);
        return room;
    }

    private void CreateRandomRooms(int maxRooms, int minSize, int maxSize, int margin)
    {
        var currentMaxSize = maxSize;
        while (currentMaxSize > minSize && rooms.Count < maxRooms)
        {
            Room? room = CreateRandomRoom(minSize, currentMaxSize, margin);

            // try again with lower maxSize
            if (room == null)
            {
                currentMaxSize--;
                continue;
            }
        }
    }

    private Room? CreateRandomRoom(int minSize, int maxSize, int margin)
    {
        // Regular hashset for increased performance
        // Only used locally, so no multi-threading risk
        var roomPositions = availablePositions.ToHashSet();
        // Extra map for faster lookup
        var roomPositionsMap = new bool[height, width];
        foreach (var (y, x) in roomPositions)
        {
            roomPositionsMap[y, x] = true;
        }

        // Choose room size
        // always odd size, so center is really center
        var rw = 1 + 2 * Rnd.Next(minSize, maxSize);
        var rh = 1 + 2 * Rnd.Next(minSize, maxSize);

        //  Include walls around room
        var areaWidth = rw + 2;
        var areaHeight = rh + 2;

        // Clear positions that would not fit room
        var clearX = (areaWidth - 1) / 2;
        var clearY = (areaHeight - 1) / 2;

        // Clear X
        for (var i = 0; i < clearX; i++)
        {
            var remove = roomPositions.
                Where(pos => (pos.x <= 0) || !roomPositionsMap[pos.y, pos.x - 1] || (pos.x >= width - 1) || !roomPositionsMap[pos.y, pos.x + 1]).
                ToList();
            foreach (var pos in remove)
            {
                roomPositions.Remove(pos);
                roomPositionsMap[pos.y, pos.x] = false;
            }
        }

        // Clear Y
        for (var i = 0; i < clearY; i++)
        {
            var remove = roomPositions.
                Where(pos => (pos.y <= 0) || !roomPositionsMap[pos.y - 1, pos.x] || (pos.y >= height - 1) || !roomPositionsMap[pos.y + 1, pos.x]).
                ToList();
            foreach (var pos in remove)
            {
                roomPositions.Remove(pos);
                roomPositionsMap[pos.y, pos.x] = false;
            }
        }

        Room? room = null;

        // Try to create new room
        if (roomPositions.Count > 0)
        {
            var (ry, rx) = roomPositions.PickOne();
            room = CreateRoom(ry, rx, rh, rw, margin);
        }

        return room;
    }

    private void ConnectRoomsRandomly()
    {
        var remaining = rooms;

        // start with top left room
        var current = remaining.OrderBy(r => r.Center.DistanceTo((0, 0))).First();

        while (remaining.Count > 1)
        {
            remaining = remaining.Remove(current);

            // pick one of the two closest rooms
            var closestRooms = remaining.OrderBy(r => r.Center.DistanceTo(current.Center)).Take(2);
            var next = closestRooms.PickOne();

            ConnectRooms(current, next);
            current = next;
        }
    }

    private void RestrictAvailablePositions(int? minY = null, int? maxY = null, int? minX = null, int? maxX = null)
    {
        Debug.Assert(previousAvailablePositions.Count == 0);
        Debug.Assert(initialRestrictedPositions.Count == 0);
        previousAvailablePositions = availablePositions;

        var _minY = minY ?? 0;
        var _maxY = maxY ?? height;
        var _minX = minX ?? 0;
        var _maxX = maxX ?? width;
        var restrictedPositions = availablePositions.Where(p => _minY <= p.y && p.y < _maxY && _minX <= p.x && p.x < _maxX).ToImmutableHashSet();
        initialRestrictedPositions = restrictedPositions;
        availablePositions = restrictedPositions;
    }

    private void RestoreAvailablePositions()
    {
        // Check what has been removed since restriction and remove it from the initial list
        var removedPositions = initialRestrictedPositions.Except(availablePositions);
        availablePositions = previousAvailablePositions.Except(removedPositions);

        previousAvailablePositions = [];
        initialRestrictedPositions = [];
    }

    private void CreateStandardMaze(bool twoPlayers = false, int minSize = 1, int maxSize = 7)
    {
        CreateRandomRooms(maxRooms: 200, minSize: minSize, maxSize: maxSize, margin: 1);
        ConnectRoomsRandomly();
        PlacePlayersTopLeftAndExitBottomRight(twoPlayers);
    }

    private void PlacePlayersTopLeftAndExitBottomRight(bool twoPlayers)
    {
        // place player in top left room
        // and exit in bottom right
        playerRoom = availableRooms.OrderBy(r => r.Center.DistanceTo((0, 0))).First();
        availableRooms = availableRooms.Remove(playerRoom);
        if (twoPlayers)
        {
            map.Player1.Position = (playerRoom.Center.y + 1, playerRoom.Center.x - 1);
            map.Player2.Position = (playerRoom.Center.y - 1, playerRoom.Center.x + 1);
        }
        else
        {
            map.Player1.Position = playerRoom.Center;
        }

        exitRoom = availableRooms.OrderBy(r => r.Center.DistanceTo((height, width))).First();
        availableRooms = availableRooms.Remove(exitRoom);
        exitPosition = (exitRoom.Bottom - 1, exitRoom.Right - 1);
        map[exitPosition] = Cell.Exit;
    }

    private (Position innerKeyPos, KeyColor innerColor, Position outerKeyPos, KeyColor outerColor) CreateDoubleLockerRoomMaze(bool twoPlayers = false)
    {
        // Need at least one big room where double locker can fit.
        // Inner locker is 3x3, outer locker is 7x7, so large room must be at least 9x9
        RestrictAvailablePositions(5, height - 5, 5, width - 5);
        var lockerRoom = CreateRandomRoom(4, 7, 1);
        Debug.Assert(lockerRoom != null);
        RestoreAvailablePositions();

        // Fill the rest with standard maze
        CreateStandardMaze(twoPlayers: twoPlayers);
        var (exitKeyColor, exitDoor) = AddLockAroundExit();

        // Create double locker room
        var (innerColor, outerColor) = CreateDoubleLockerRoom(lockerRoom, exitKeyColor);

        // Place inner key in same room as player so it can accidentally be picked up
        var innerKeyPos = (playerRoom.Bottom - 1, playerRoom.Right - 1);

        // Place outer key far from exit and player
        var outerKeyPos = ClaimRandomPositionInAvailableRoomFarthestFrom([map.Player1.Position, exitDoor]);

        return (innerKeyPos, innerColor, outerKeyPos, outerColor);
    }

    private void CreateHorizontalTunnel(int y, int x1, int x2, int dir)
    {
        if (x1 == x2) return;
        var start = Math.Min(x1, x2);
        var end = Math.Max(x1, x2);
        for (var x = start; x <= end; x++)
        {
            SetAndMakeUnavailable((y, x), Cell.Empty);
            SetAndMakeUnavailable((y - dir, x), Cell.Empty);
        }
    }

    private void CreateVerticalTunnel(int y1, int y2, int x, int dir)
    {
        if (y1 == y2) return;
        var start = Math.Min(y1, y2);
        var end = Math.Max(y1, y2);
        for (var y = start; y <= end; y++)
        {
            SetAndMakeUnavailable((y, x), Cell.Empty);
            SetAndMakeUnavailable((y, x - dir), Cell.Empty);
        }
    }

    private void ConnectRooms(Room room1, Room room2)
    {
        var dx = room2.X - room1.X;
        var dy = room2.Y - room1.Y;

        if (Math.Abs(dx) > Math.Abs(dy))
        {
            CreateHorizontalTunnel(room1.Y, room1.X, room2.X, Math.Sign(dx));
            CreateVerticalTunnel(room1.Y, room2.Y, room2.X, Math.Sign(dy));
        }
        else
        {
            CreateVerticalTunnel(room1.Y, room2.Y, room1.X, Math.Sign(dy));
            CreateHorizontalTunnel(room2.Y, room1.X, room2.X, Math.Sign(dx));
        }
    }

    private (KeyColor keyColor, Position doorPos) AddLockAroundExit()
    {
        // pick a random key color
        var keyColor = PickRandomAvailableKeyColor();

        // Place wall of doors around exit
        Position doorPos = new();
        for (var y = exitPosition.y - 1; y <= exitPosition.y + 1; y++)
        {
            for (var x = exitPosition.x - 1; x <= exitPosition.x + 1; x++)
            {
                if (0 <= y && y < height && 0 <= x && x < width)
                {
                    if (SetIfEmpty(y, x, ToDoor(keyColor)))
                    {
                        doorPos = (y, x);
                    }
                }
            }
        }

        return (keyColor, doorPos);
    }

    private Position PickRandomDoorPosForSmallRoom(Position roomCenter)
    {
        var posibleDoorPositions = new List<Position>();
        if (roomCenter.y < height - 2 && map[roomCenter.y + 2, roomCenter.x] == Cell.Empty)
        {
            posibleDoorPositions.Add((roomCenter.y + 1, roomCenter.x));
        }
        if (roomCenter.y > 2 && map[roomCenter.y - 2, roomCenter.x] == Cell.Empty)
        {
            posibleDoorPositions.Add((roomCenter.y - 1, roomCenter.x));
        }
        if (roomCenter.x < width - 2 && map[roomCenter.y, roomCenter.x + 2] == Cell.Empty)
        {
            posibleDoorPositions.Add((roomCenter.y, roomCenter.x + 1));
        }
        if (roomCenter.x > 2 && map[roomCenter.y, roomCenter.x - 2] == Cell.Empty)
        {
            posibleDoorPositions.Add((roomCenter.y, roomCenter.x - 1));
        }
        return posibleDoorPositions.PickOne();
    }

    private static Cell ToDoor(KeyColor color) => color switch
    {
        KeyColor.Red => Cell.DoorRedClosed,
        KeyColor.Green => Cell.DoorGreenClosed,
        KeyColor.Blue => Cell.DoorBlueClosed,
        _ => throw new NotImplementedException(),
    };

    private static Cell ToKey(KeyColor color) => color switch
    {
        KeyColor.Red => Cell.KeyRed,
        KeyColor.Green => Cell.KeyGreen,
        KeyColor.Blue => Cell.KeyBlue,
        _ => throw new NotImplementedException(),
    };

    private static Cell ToPressurePlate(KeyColor color) => color switch
    {
        KeyColor.Red => Cell.PressurePlateRed,
        KeyColor.Green => Cell.PressurePlateGreen,
        KeyColor.Blue => Cell.PressurePlateBlue,
        _ => throw new NotImplementedException(),
    };

    private static Inventory ToInventory(KeyColor color) => color switch
    {
        KeyColor.Red => Inventory.KeyRed,
        KeyColor.Green => Inventory.KeyGreen,
        KeyColor.Blue => Inventory.KeyBlue,
        _ => throw new NotImplementedException(),
    };

    private (IImmutableDictionary<Position, int> distances, IImmutableDictionary<Position, Position> paths) ComputeDistancesFrom(Position fromPos)
    {
        // Regular containers for performance
        var distances = new Dictionary<Position, int>();
        var paths = new Dictionary<Position, Position>();
        var todo = new Queue<Position>();

        void CheckAndAdd(Position currentPos, int currentDist, Position nextPos)
        {
            if (!map[nextPos].CanWalkOn()) return;

            var nextDist = distances.TryGetValue(nextPos, out var d) ? d : int.MaxValue;
            if (currentDist + 1 < nextDist)
            {
                distances[nextPos] = currentDist + 1;
                paths[nextPos] = currentPos;
                todo.Enqueue(nextPos);
            }
        }

        distances.Add(fromPos, 0);
        todo.Enqueue(fromPos);

        while (todo.TryDequeue(out var currentPos))
        {
            var currentDist = distances[currentPos];

            if (currentPos.y > 0) CheckAndAdd(currentPos, currentDist, (currentPos.y - 1, currentPos.x));
            if (currentPos.y < height - 1) CheckAndAdd(currentPos, currentDist, (currentPos.y + 1, currentPos.x));
            if (currentPos.x > 0) CheckAndAdd(currentPos, currentDist, (currentPos.y, currentPos.x - 1));
            if (currentPos.x < width - 1) CheckAndAdd(currentPos, currentDist, (currentPos.y, currentPos.x + 1));
        }

        return (distances.ToImmutableDictionary(), paths.ToImmutableDictionary());
    }

    private Position ClaimRandomPositionInAvailableRoomFarthestFrom(Position[] inputPositions, int margin = 0, int minRoomHeight = 1, int minRoomWidth = 1)
    {
        var rooms = availableRooms.Where(r => r.Height >= minRoomHeight && r.Width >= minRoomWidth);
        return ClaimRandomPositionInRoomFarthestFrom(rooms, inputPositions, margin);
    }

    private Position ClaimRandomPositionInRoomFarthestFrom(IEnumerable<Room> rooms, Position[] inputPositions, int margin = 0)
    {
        var room = GetRoomFarthestPositionFrom(rooms, inputPositions);
        availableRooms = availableRooms.Remove(room);
        return GetRandomEmptyPositionInRoom(room, margin);
    }

    private Position GetRandomEmptyPositionInRoom(Room room, int margin = 0)
    {
        return room.GetPositions(margin).Where(IsEmpty).PickOne();
    }

    private Position? TryGetRandomEmptyPositionInRoom(Room room, int margin = 0)
    {
        var positions = room.GetPositions(margin).Where(IsEmpty).ToList();
        if (positions.Count == 0) return null;
        return positions.PickOne();
    }

    private bool IsEmpty(Position pos)
    {
        return map[pos] == Cell.Empty &&
            !(map.Player1.Position.IsValid() && map.Player1.Position.Equals(pos)) &&
            !(map.Player2.Position.IsValid() && map.Player2.Position.Equals(pos)) &&
            !(map.Enemy1.Position.IsValid() && map.Enemy1.Position.Equals(pos)) &&
            !(map.Enemy2.Position.IsValid() && map.Enemy2.Position.Equals(pos));
    }

    private Room GetRoomFarthestPositionFrom(IEnumerable<Room> rooms, params Position[] inputPositions)
    {
        // Compute distances from each input position
        var inputDistances = ImmutableList<IImmutableDictionary<Position, int>>.Empty;
        foreach (var pos in inputPositions)
        {
            var (distances, _) = ComputeDistancesFrom(pos);
            inputDistances = inputDistances.Add(distances);
        }

        int bestDistance = int.MinValue;
        var bestRooms = ImmutableHashSet<Room>.Empty;
        foreach (var room in rooms)
        {
            var center = room.Center;
            // Get distance to this room from all input positions
            var roomDistances = inputDistances.Where(d => d.ContainsKey(center)).Select(d => d[center]).ToImmutableArray();
            // Check if it reachable from all input points
            if (roomDistances.Length != inputDistances.Count) continue;

            var distance = roomDistances.Aggregate(1, (agg, dist) => agg * dist);

            if (distance > bestDistance)
            {
                bestDistance = distance;
                bestRooms = [room];
            }
            else if (distance == bestDistance)
            {
                bestRooms = bestRooms.Add(room);
            }
        }
        return bestRooms.PickOne();
    }

    private (int y, int x) ClaimRandomPositionInRandomAvailableRoom(IEnumerable<Room> rooms, int margin = 0)
    {
        var room = rooms.Where(r => availableRooms.Contains(r)).PickOne();
        availableRooms = availableRooms.Remove(room);
        return GetRandomEmptyPositionInRoom(room, margin);
    }

    private Room ClaimClosestAvailableRoomFrom(Position pos, int minRoomHeight = 1, int minRoomWidth = 1)
    {
        var (distances, _) = ComputeDistancesFrom(pos);

        int minDistance = int.MaxValue;
        var minRooms = ImmutableHashSet<Room>.Empty;

        foreach (var room in availableRooms.Where(r => r.Height >= minRoomHeight && r.Width >= minRoomWidth))
        {
            if (distances.TryGetValue(room.Center, out var dist))
            {
                if (dist < minDistance)
                {
                    minDistance = dist;
                    minRooms = [room];
                }
                else if (dist == minDistance)
                {
                    minRooms = minRooms.Add(room);
                }
            }
        }

        var minRoom = minRooms.PickOne();
        availableRooms = availableRooms.Remove(minRoom);
        return minRoom;
    }

    private Position? GetEmptyPositionInFront(Position pos)
    {
        ImmutableList<Position> positions = [];

        if (pos.y > 0 && map[pos.y - 1, pos.x] == Cell.Empty) positions = positions.Add((pos.y - 1, pos.x));
        if (pos.y < height && map[pos.y + 1, pos.x] == Cell.Empty) positions = positions.Add((pos.y + 1, pos.x));
        if (pos.x > 0 && map[pos.y, pos.x - 1] == Cell.Empty) positions = positions.Add((pos.y, pos.x - 1));
        if (pos.x < width && map[pos.y, pos.x + 1] == Cell.Empty) positions = positions.Add((pos.y, pos.x + 1));

        return positions.Count > 0 ? positions.PickOne() : null;
    }

    private (KeyColor lockerKeyColor, (int y, int x) infrontDoorPos) AddLocker(Position lockerCenter, KeyColor keyColor)
    {
        return AddLocker(lockerCenter, ToKey(keyColor));
    }

    private (KeyColor lockerKeyColor, (int y, int x) infrontDoorPos) AddLocker(Position lockerCenter, Cell content)
    {
        // Create locker room (3x3) with center at given position
        map[lockerCenter] = content;
        for (var y = lockerCenter.y - 1; y <= lockerCenter.y + 1; y++)
        {
            for (var x = lockerCenter.x - 1; x <= lockerCenter.x + 1; x++)
            {
                SetIfEmpty(y, x, Cell.Wall);
            }
        }

        // Add a door to the locker room
        var lockerDoorPos = PickRandomDoorPosForSmallRoom(lockerCenter);
        var lockerKeyColor = PickRandomAvailableKeyColor();
        map[lockerDoorPos] = ToDoor(lockerKeyColor);

        // Get in front of door pos
        var dy = lockerDoorPos.y - lockerCenter.y;
        var dx = lockerDoorPos.x - lockerCenter.x;
        var infronLockerDoorPos2 = (lockerCenter.y + dy * 2, lockerCenter.x + dx * 2);

        var infrontLockerDoorPos = GetEmptyPositionInFront(lockerDoorPos) ?? throw new MapGeneratorException("Locker door not placed correctly");
        Debug.Assert(infrontLockerDoorPos == infronLockerDoorPos2);
        Debug.Assert(map[infrontLockerDoorPos] == Cell.Empty);
        return (lockerKeyColor, infrontLockerDoorPos);
    }

    private (int middle, ImmutableList<Room> roomsLeft, ImmutableList<Room> roomsRight) CreateSplitMaze(bool twoPlayers = false)
    {
        var middle = width / 2;

        // Create left and right rooms
        rooms = [];
        RestrictAvailablePositions(maxX: middle);
        CreateRandomRooms(maxRooms: 100, minSize: 1, maxSize: 5, margin: 1);
        RestoreAvailablePositions();
        ConnectRoomsRandomly();
        var roomsLeft = rooms;

        rooms = [];
        RestrictAvailablePositions(minX: middle + 1);
        CreateRandomRooms(maxRooms: 100, minSize: 1, maxSize: 5, margin: 1);
        RestoreAvailablePositions();
        ConnectRoomsRandomly();
        var roomsRight = rooms;

        rooms = roomsLeft.AddRange(roomsRight);

        PlacePlayersTopLeftAndExitBottomRight(twoPlayers);

        return (middle, roomsLeft, roomsRight);
    }

    private int ConnectLeftAndRightWithDoor(int middle, ImmutableList<Room> roomsLeft, ImmutableList<Room> roomsRight, KeyColor doorColor)
    {
        var tunnelPositions = ConnectLeftAndRight(middle, roomsLeft, roomsRight);

        // Create door in tunnel between left and right
        var door = ToDoor(doorColor);
        foreach (var (y, x) in tunnelPositions)
        {
            SetIfEmpty(y, x, door);
        }

        // Return a position of the door where left and right side are both empty
        var doorPosition = tunnelPositions.
            Where(pos => map[pos.y, pos.x - 1] == Cell.Empty && map[pos.y, pos.x + 1] == Cell.Empty).
            PickOne();
        return doorPosition.y;
    }

    private ImmutableList<Position> ConnectLeftAndRight(int middle, ImmutableList<Room> roomsLeft, ImmutableList<Room> roomsRight)
    {
        // Connect left and right rooms closest to each other
        var minDist = double.PositiveInfinity;
        Room? minLeft = null;
        Room? minRight = null;
        foreach (var left in roomsLeft.Where(r => availableRooms.Contains(r)))
        {
            foreach (var right in roomsRight.Where(r => availableRooms.Contains(r)))
            {
                var dist = left.Center.DistanceTo(right.Center);
                if (dist < minDist)
                {
                    minDist = dist;
                    minLeft = left;
                    minRight = right;
                }
            }
        }
        Debug.Assert(minLeft != null && minRight != null);

        ConnectRooms(minLeft, minRight);

        // Don't use them for enemy or keys
        availableRooms = availableRooms.Remove(minLeft).Remove(minRight);

        // Find position of tunnel middle
        var tunnelPositions = ImmutableList<Position>.Empty;
        var top = Math.Min(minLeft.Top, minRight.Top);
        var bottom = Math.Max(minLeft.Bottom, minRight.Bottom);
        for (var y = top; y < bottom; y++)
        {
            if (map[y, middle] == Cell.Empty)
            {
                tunnelPositions = tunnelPositions.Add((y, middle));
            }
        }

        return tunnelPositions;
    }

    bool SetIfEmpty(int y, int x, Cell value)
    {
        if (map[y, x] != Cell.Empty) return false;
        map[y, x] = value;
        return true;
    }

    void SetAndMakeUnavailable(Position pos, Cell value)
    {
        map[pos] = value;
        availablePositions = availablePositions.Remove(pos);
    }

    private KeyColor CreateChamber(Position center, int height, int width)
    {
        var top = center.y - height / 2;
        var bottom = top + height - 1;
        var left = center.x - width / 2;
        var right = left + width - 1;

        for (var x = left; x <= right; x++)
        {
            SetIfEmpty(top, x, Cell.Wall);
            SetIfEmpty(bottom, x, Cell.Wall);
        }
        for (var y = top; y <= bottom; y++)
        {
            SetIfEmpty(y, left, Cell.Wall);
            SetIfEmpty(y, right, Cell.Wall);
        }

        var chamberKeyColor = PickRandomAvailableKeyColor();

        var doorCell = ToDoor(chamberKeyColor);
        int direction = Directions.PickOne();
        switch (direction)
        {
            case 'N':
                for (var x = left + 1; x < right; x++) map[top, x] = doorCell;
                break;
            case 'E':
                for (var y = top + 1; y < bottom; y++) map[y, right] = doorCell;
                break;
            case 'S':
                for (var x = left + 1; x < right; x++) map[bottom, x] = doorCell;
                break;
            case 'W':
                for (var y = top + 1; y < bottom; y++) map[y, left] = doorCell;
                break;

            default: throw new InvalidOperationException();
        }

        return chamberKeyColor;
    }

    private (KeyColor innerKeyColor, KeyColor outerKeyColor) CreateDoubleLockerRoom(Room lockerRoom, KeyColor lockedKeyColor)
    {
        // Make sure locker room is claimed
        availableRooms = availableRooms.Remove(lockerRoom);

        var (cy, cx) = lockerRoom.Center;

        map[cy, cx] = ToKey(lockedKeyColor);
        for (int y = cy - 1; y <= cy + 1; y++)
        {
            for (int x = cx - 1; x <= cx + 1; x++)
            {
                SetIfEmpty(y, x, Cell.Wall);
            }
        }

        for (int x = cx - 3; x <= cx + 3; x++)
        {
            SetIfEmpty(cy - 3, x, Cell.Wall);
            SetIfEmpty(cy + 3, x, Cell.Wall);
        }
        for (int y = cy - 2; y <= cy + 2; y++)
        {
            SetIfEmpty(y, cx - 3, Cell.Wall);
            SetIfEmpty(y, cx + 3, Cell.Wall);
        }

        var innerKeyColor = PickRandomAvailableKeyColor();
        map[cy - 1, cx] = ToDoor(innerKeyColor);

        var outerKeyColor = PickRandomAvailableKeyColor();
        map[cy + 3, cx] = ToDoor(outerKeyColor);

        return (innerKeyColor, outerKeyColor);
    }

    private KeyColor PickRandomAvailableKeyColor()
    {
        var keyColor = availableKeyColors.PickOne();
        availableKeyColors = availableKeyColors.Remove(keyColor);
        return keyColor;
    }

    private int? FindHorizontalTunnel(int y1, int y2, int x)
    {
        for (var y = y1; y < y2; y++)
        {
            if (map[y, x] == Cell.Unknown && map[y + 1, x] == Cell.Empty &&
                map[y + 2, x] == Cell.Empty && map[y + 3, x] == Cell.Unknown)
            {
                return y + 1;
            }
        }

        return null;
    }

    void PlaceRandomly(Cell item)
    {
        for (var i = 0; i < 10; i++)
        {
            var itemRoom = availableRooms.PickOne();
            var itemPos = TryGetRandomEmptyPositionInRoom(itemRoom, margin: 1);
            if (itemPos.HasValue)
            {
                map[itemPos.Value] = item;
                return;
            }
        }
        throw new MapGeneratorException("No random position found");
    }
}