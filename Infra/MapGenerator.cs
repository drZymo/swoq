namespace Swoq.Infra;

using System.Collections.Immutable;
using System.Diagnostics;
using Position = (int y, int x);

// TODO: subclasses per level?
public class MapGenerator : IMapGenerator
{
    private class MapGeneratorException(string message) : Exception(message) { }

    private static readonly char[] Directions = ['N', 'E', 'S', 'W'];
    private const int MaxRetries = 10;

    private readonly int height;
    private readonly int width;

    private readonly IImmutableSet<Position> allPositions = ImmutableHashSet<Position>.Empty;
    private MutableMap map;

    private Position exitPosition;

    private ImmutableList<Room> rooms = [];
    private ImmutableHashSet<Room> availableRooms = [];
    private enum KeyColor { Red, Green, Blue }
    private ImmutableHashSet<KeyColor> availableKeyColors = [KeyColor.Red, KeyColor.Green, KeyColor.Blue];

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

                RemoveInnerWalls();

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

    private void Reset(int level)
    {
        map = new(level, height, width);

        exitPosition = new();

        rooms = [];
        availableRooms = [];
        availableKeyColors = [KeyColor.Red, KeyColor.Green, KeyColor.Blue];

        // Fill with walls
        foreach (var pos in allPositions)
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
        /// First combat.
        /// Key in left side for door to enter right side.
        /// One sword and armor in left side.
        /// One enemy in right side.
        /// Enemy has key to exit door

        var (middle, roomsLeft, roomsRight) = CreateSplitMaze();

        var (exitKeyColor, _) = AddLockAroundExit();

        // Create tunnel between left and right with a door
        var keyColor = PickRandomAvailableKeyColor();
        var doorPosY = ConnectLeftAndRightWithDoor(middle, roomsLeft, roomsRight, keyColor);
        var infrontOfDoor = (doorPosY, middle - 1);

        var keyPos = ClaimRandomPositionInAvailableRoomFarthestFrom([map.Player1.Position, infrontOfDoor]);
        map[keyPos] = ToKey(keyColor);

        // Place sword in any room on the left
        var swordPos = ClaimRandomPositionInRandomAvailableRoom(roomsLeft);
        map[swordPos] = Cell.Sword;

        // Place health in any room on the left
        var healthPos = ClaimRandomPositionInRandomAvailableRoom(roomsLeft);
        map[healthPos] = Cell.Health;

        // Place enemy in any room on the right with key to exit
        var enemyPos = ClaimRandomPositionInRandomAvailableRoom(roomsRight);
        map.Enemy1.Position = enemyPos;
        map.Enemy1.Inventory = ToInventory(exitKeyColor);
    }

    private void GenerateLevel9()
    {
        /// Prison
        /// One big room that holds the second player.
        /// Guard has key to prison.
        /// Exit is open.
        /// Health and sword spread around map.

        // Need at least one big room that can fit prison
        var prisonRoom = CreateRandomRoom(11, 15, 0, 5, height - 5, 5, width - 5);
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

    private void GenerateLevel10()
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

    private void GenerateLevel11()
    {
        /// Double-door locker room.
        /// With two players.
        /// Again correct key must be picked up first.

        var (innerKeyPos, innerColor, outerKeyPos, outerColor) = CreateDoubleLockerRoomMaze(twoPlayers: true);

        map[innerKeyPos] = ToKey(innerColor);
        map[outerKeyPos] = ToKey(outerColor);
    }

    private void GenerateLevel12()
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

        // Put exit key on random room in the left
        (int y, int x) exitKeyPos = ClaimRandomPositionInRandomAvailableRoom(roomsLeft);
        map[exitKeyPos] = ToKey(exitKeyColor);
    }

    private void GenerateLevel13()
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

    private void GenerateLevel14()
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

    private void GenerateLevel15()
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

    private void GenerateLevel16()
    {
        /// Two enemies.
        /// One enemy on left side, which has the key for right side.
        /// Right enemy has key for exit.
        /// One sword and armor on the left side (one player has to catch them both and attack), one sword and armor on the right side, which the other players has to get and use.

        var (middle, roomsLeft, roomsRight) = CreateSplitMaze(twoPlayers: true);

        var tunnelKeyColor = PickRandomAvailableKeyColor();
        var tunnelPosY = ConnectLeftAndRightWithDoor(middle, roomsLeft, roomsRight, tunnelKeyColor);
        var infrontTunnelDoorLeft = (tunnelPosY, middle - 1);
        var infrontTunnelDoorRight = (tunnelPosY, middle + 1);

        var (exitKeyColor, exitDoorPos) = AddLockAroundExit();

        // Enemies close to doors
        var enemy1Room = ClaimClosestAvailableRoomFrom(infrontTunnelDoorLeft);
        var enemy1Pos = GetRandomEmptyPositionInRoom(enemy1Room);
        map.Enemy1.Position = enemy1Pos;
        map.Enemy1.Inventory = ToInventory(tunnelKeyColor);

        var enemy2Room = ClaimClosestAvailableRoomFrom(exitDoorPos);
        var enemy2Pos = GetRandomEmptyPositionInRoom(enemy2Room);
        map.Enemy2.Position = enemy2Pos;
        map.Enemy2.Inventory = ToInventory(exitKeyColor);

        // One health and sword left
        var health1Pos = ClaimRandomPositionInAvailableRoomFarthestFrom([map.Player1.Position, map.Enemy1.Position]);
        map[health1Pos] = Cell.Health;
        var sword1Pos = ClaimRandomPositionInAvailableRoomFarthestFrom([map.Player1.Position, map.Enemy1.Position]);
        map[sword1Pos] = Cell.Sword;

        // One health and sword right
        var health2Pos = ClaimRandomPositionInAvailableRoomFarthestFrom([infrontTunnelDoorRight, map.Enemy2.Position]);
        map[health2Pos] = Cell.Health;
        var sword2Pos = ClaimRandomPositionInAvailableRoomFarthestFrom([infrontTunnelDoorRight, map.Enemy2.Position]);
        map[sword2Pos] = Cell.Sword;
    }

    private Room CreateRoom(int y, int x, int height, int width)
    {
        var room = new Room(y, x, height, width);

        // Make empty
        for (var my = room.Top; my < room.Bottom; my++)
            for (var mx = room.Left; mx < room.Right; mx++)
                map[my, mx] = Cell.Empty;

        rooms = rooms.Add(room);
        availableRooms = availableRooms.Add(room);
        return room;
    }

    private void CreateRandomRooms(int maxRooms, int minSize, int maxSize, int margin,
        int? minY = null, int? maxY = null, int? minX = null, int? maxX = null)
    {
        var _minY = minY ?? 0;
        var _maxY = maxY ?? height;
        var _minX = minX ?? 0;
        var _maxX = maxX ?? width;

        var currentMaxSize = maxSize;
        while (currentMaxSize > minSize && rooms.Count < maxRooms)
        {
            Room? newRoom = CreateRandomRoom(minSize, currentMaxSize, margin, _minY, _maxY, _minX, _maxX);

            if (newRoom == null)
            {
                currentMaxSize--;
                continue;
            }
        }
    }

    private Room? CreateRandomRoom(int minSize, int maxSize, int margin, int minY, int maxY, int minX, int maxX)
    {
        var rw = Rnd.Next(minSize, maxSize);
        var rh = Rnd.Next(minSize, maxSize);

        var choices = allPositions.Where(p => minY <= p.y && p.y < maxY && minX <= p.x && p.x < maxX).ToImmutableHashSet();

        // clear out edges
        Debug.Assert(minY + 1 + rh / 2 <= maxY);
        for (var y = minY; y < minY + 1 + rh / 2; y++)
        {
            for (var x = minX; x < maxX; x++)
            {
                choices = choices.Remove((y, x));
            }
        }
        for (var y = maxY - (rh - rh / 2); y < maxY; y++)
        {
            for (var x = minX; x < maxX; x++)
            {
                choices = choices.Remove((y, x));
            }
        }
        Debug.Assert(minX + 1 + rw / 2 <= maxX);
        for (var x = minX; x < minX + 1 + rw / 2; x++)
        {
            for (var y = minY; y < maxY; y++)
            {
                choices = choices.Remove((y, x));
            }
        }
        for (var x = maxX - (rw - rw / 2); x < maxX; x++)
        {
            for (var y = minY; y < maxY; y++)
            {
                choices = choices.Remove((y, x));
            }
        }

        // clear out other rooms
        foreach (var room in rooms)
        {
            var top = room.Top - margin - (rh - rh / 2);
            var bottom = room.Bottom + margin + rh / 2;
            var left = room.Left - margin - (rw - rw / 2);
            var right = room.Right + margin + rw / 2;

            for (var y = top; y < bottom; y++)
            {
                for (var x = left; x < right; x++)
                {
                    choices = choices.Remove((y, x));
                }
            }
        }

        Room? newRoom = null;

        if (choices.Count > 0)
        {
            var (ry, rx) = choices.PickOne();
            newRoom = CreateRoom(ry, rx, rh, rw);
        }

        return newRoom;
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

    private void CreateStandardMaze(bool twoPlayers = false)
    {
        CreateRandomRooms(30, 3, 15, 1);
        ConnectRoomsRandomly();
        PlacePlayersTopLeftAndExitBottomRight(twoPlayers);
    }

    private void PlacePlayersTopLeftAndExitBottomRight(bool twoPlayers)
    {
        // place player in top left room
        // and exit in bottom right
        var playerRoom = availableRooms.OrderBy(r => r.Center.DistanceTo((0, 0))).First();
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

        var exitRoom = availableRooms.OrderBy(r => r.Center.DistanceTo((height, width))).First();
        availableRooms = availableRooms.Remove(exitRoom);
        exitPosition = (exitRoom.Bottom - 1, exitRoom.Right - 1);
        map[exitPosition] = Cell.Exit;
    }

    private (Position innerKeyPos, KeyColor innerColor, Position outerKeyPos, KeyColor outerColor) CreateDoubleLockerRoomMaze(bool twoPlayers = false)
    {
        // Need at least one big room where double locker can fit.
        // Inner locker is 3x3, outer locker is 7x7, so large room must be at least 9x9
        var lockerRoom = CreateRandomRoom(9, 15, 1, 5, height - 5, 5, width - 5);
        Debug.Assert(lockerRoom != null);

        // Fill the rest with standard maze
        CreateStandardMaze(twoPlayers: twoPlayers);
        var (exitKeyColor, exitDoor) = AddLockAroundExit();

        // Create double locker room
        var (innerColor, outerColor) = CreateDoubleLockerRoom(lockerRoom, exitKeyColor);

        // Place inner key in room closest to player so it can accidentally be picked up
        var innerKeyRoom = ClaimClosestAvailableRoomFrom(map.Player1.Position);
        var innerKeyPos = GetRandomEmptyPositionInRoom(innerKeyRoom, 1);

        // Place outer key far from exit and player
        var outerKeyPos = ClaimRandomPositionInAvailableRoomFarthestFrom([map.Player1.Position, exitDoor]);

        return (innerKeyPos, innerColor, outerKeyPos, outerColor);
    }

    private void DrawHLine(int y, int x1, int x2, int dir, Cell value = Cell.Empty)
    {
        if (x1 == x2) return;
        var start = Math.Min(x1, x2);
        var end = Math.Max(x1, x2);
        for (var x = start; x <= end; x++)
        {
            map[y, x] = value;
            map[y - dir, x] = value;
        }
    }

    private void DrawVLine(int y1, int y2, int x, int dir, Cell value = Cell.Empty)
    {
        if (y1 == y2) return;
        var start = Math.Min(y1, y2);
        var end = Math.Max(y1, y2);
        for (var y = start; y <= end; y++)
        {
            map[y, x] = value;
            map[y, x - dir] = value;
        }
    }

    private void ConnectRooms(Room room1, Room room2)
    {
        var dx = room2.X - room1.X;
        var dy = room2.Y - room1.Y;

        if (Math.Abs(dx) > Math.Abs(dy))
        {
            DrawHLine(room1.Y, room1.X, room2.X, Math.Sign(dx));
            DrawVLine(room1.Y, room2.Y, room2.X, Math.Sign(dy));
        }
        else
        {
            DrawVLine(room1.Y, room2.Y, room1.X, Math.Sign(dy));
            DrawHLine(room2.Y, room1.X, room2.X, Math.Sign(dx));
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
        var distances = ImmutableDictionary<Position, int>.Empty;
        var paths = ImmutableDictionary<Position, Position>.Empty;
        var todo = ImmutableQueue<Position>.Empty;

        void CheckAndAdd(Position currentPos, int currentDist, Position nextPos)
        {
            if (!map[nextPos].CanWalkOn()) return;

            var nextDist = distances.TryGetValue(nextPos, out var d) ? d : int.MaxValue;
            if (currentDist + 1 < nextDist)
            {
                distances = distances.SetItem(nextPos, currentDist + 1);
                paths = paths.SetItem(nextPos, currentPos);
                todo = todo.Enqueue(nextPos);
            }
        }

        distances = distances.Add(fromPos, 0);
        todo = todo.Enqueue(fromPos);

        while (!todo.IsEmpty)
        {
            todo = todo.Dequeue(out var currentPos);
            var currentDist = distances[currentPos];

            if (currentPos.y > 0) CheckAndAdd(currentPos, currentDist, (currentPos.y - 1, currentPos.x));
            if (currentPos.y < height - 1) CheckAndAdd(currentPos, currentDist, (currentPos.y + 1, currentPos.x));
            if (currentPos.x > 0) CheckAndAdd(currentPos, currentDist, (currentPos.y, currentPos.x - 1));
            if (currentPos.x < width - 1) CheckAndAdd(currentPos, currentDist, (currentPos.y, currentPos.x + 1));
        }

        return (distances, paths);
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
        if (pos.y > 0 && map[pos.y - 1, pos.x] == Cell.Empty) return (pos.y - 1, pos.x);
        if (pos.y < height && map[pos.y + 1, pos.x] == Cell.Empty) return (pos.y + 1, pos.x);
        if (pos.x > 0 && map[pos.y, pos.x - 1] == Cell.Empty) return (pos.y, pos.x - 1);
        if (pos.x < width && map[pos.y, pos.x + 1] == Cell.Empty) return (pos.y, pos.x + 1);

        // TODO: Random?
        return null;
    }

    private (KeyColor lockerKeyColor, (int y, int x) infrontDoorPos) AddLocker(Position lockerCenter, KeyColor keyColor)
    {
        // Create locker room (3x3) with center at given position
        map[lockerCenter] = ToKey(keyColor);
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
        // Create left and right rooms
        var middle = width / 2;
        rooms = [];
        CreateRandomRooms(15, 3, 12, 2, maxX: middle);
        ConnectRoomsRandomly();
        var roomsLeft = rooms;
        rooms = [];
        CreateRandomRooms(15, 3, 12, 2, minX: middle + 1);
        ConnectRoomsRandomly();
        var roomsRight = rooms;
        rooms = roomsLeft.AddRange(roomsRight);

        PlacePlayersTopLeftAndExitBottomRight(twoPlayers);

        return (middle, roomsLeft, roomsRight);
    }

    private int ConnectLeftAndRightWithDoor(int middle, ImmutableList<Room> roomsLeft, ImmutableList<Room> roomsRight, KeyColor doorColor)
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

        // Create door in tunnel between left and right
        var door = ToDoor(doorColor);
        var doorPositions = ImmutableList<int>.Empty;
        var top = Math.Min(minLeft.Top, minRight.Top);
        var bottom = Math.Max(minLeft.Bottom, minRight.Bottom);
        for (var y = top; y < bottom; y++)
        {
            if (SetIfEmpty(y, middle, door))
            {
                doorPositions = doorPositions.Add(y);
            }
        }

        // Return a position of the door where left and right side are both empty
        int doorPosition = doorPositions.
            Where(y => map[y, middle - 1] == Cell.Empty && map[y, middle + 1] == Cell.Empty).
            PickOne();
        return doorPosition;
    }

    private void RemoveInnerWalls()
    {
        var innerWalls = ImmutableList<Position>.Empty;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var isInner = true;
                if (y > 1)
                {
                    if (x > 1) isInner = isInner && (map[y - 1, x - 1] == Cell.Wall);
                    isInner = isInner && (map[y - 1, x] == Cell.Wall);
                    if (x < width - 1) isInner = isInner && (map[y - 1, x + 1] == Cell.Wall);
                }

                if (x > 1) isInner = isInner && (map[y, x - 1] == Cell.Wall);
                isInner = isInner && (map[y, x] == Cell.Wall);
                if (x < width - 1) isInner = isInner && (map[y, x + 1] == Cell.Wall);

                if (y < height - 1)
                {
                    if (x > 1) isInner = isInner && (map[y + 1, x - 1] == Cell.Wall);
                    isInner = isInner && (map[y + 1, x] == Cell.Wall);
                    if (x < width - 1) isInner = isInner && (map[y + 1, x + 1] == Cell.Wall);
                }

                if (isInner)
                {
                    innerWalls = innerWalls.Add((y, x));
                }
            }
        }

        foreach (var (y, x) in innerWalls)
        {
            map[y, x] = Cell.Unknown;
        }
    }

    bool SetIfEmpty(int y, int x, Cell value)
    {
        if (map[y, x] != Cell.Empty) return false;
        map[y, x] = value;
        return true;
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
}