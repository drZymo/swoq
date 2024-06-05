namespace Swoq.Infra;

using System.Collections.Immutable;
using System.Diagnostics;
using Position = (int y, int x);

public class MapGenerator
{
    private class MapGeneratorException(string message) : Exception(message) { }

    private static readonly Random random = new();
    private static readonly char[] Directions = ['N', 'E', 'S', 'W'];

    private readonly int height;
    private readonly int width;
    private readonly MutableMap map;

    private Position exitPosition;

    private IImmutableList<Room> rooms = ImmutableList<Room>.Empty;
    private readonly IImmutableSet<Position> allPosition = ImmutableHashSet<Position>.Empty;

    private enum KeyColor { Red, Green, Blue }

    private IImmutableSet<KeyColor> availableKeyColors = [KeyColor.Red, KeyColor.Green, KeyColor.Blue];
    private IImmutableSet<Room> availableRooms = ImmutableHashSet<Room>.Empty;

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

    private MapGenerator(int height, int width)
    {
        this.height = height;
        this.width = width;
        map = new(height, width);

        // Fill with walls
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                map[y, x] = Cell.Wall;
                allPosition = allPosition.Add((y, x));
            }
        }
    }

    private Map Generate(int level)
    {
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

        RemoveInnerWalls();

        if (map[map.Player1.Position] != Cell.Empty) throw new MapGeneratorException("Player 1 position invalid");
        if (map[map.Player2.Position] != Cell.Empty) throw new MapGeneratorException("Player 2 position invalid");

        return map.ToMap();
    }

    private void GenerateLevel0()
    {
        // TODO: subclasses per level?
        CreateRoom(32, 32, 10, 10);
        map.Player1.Position = (32 - 5, 32 - 5);
        map[32 + 4, 32 + 5] = Cell.Exit;
    }

    private void GenerateLevel1()
    {
        CreateRandomRooms(2, 3, 10, 8);
        ConnectRoomsRandomly();

        PlacePlayerTopLeftAndExitBottomRight();
    }

    private void GenerateLevel2()
    {
        CreateRandomRooms(30, 3, 8, 3);
        ConnectRoomsRandomly();

        PlacePlayerTopLeftAndExitBottomRight();
    }

    private void GenerateLevel3()
    {
        CreateRandomRooms(30, 3, 15, 1);
        ConnectRoomsRandomly();
        PlacePlayerTopLeftAndExitBottomRight();

        // Add exit door
        var (keyColor, doorPos) = AddLockAroundExit();
        var infrontDoorPos = GetEmptyPositionInFront(doorPos) ?? throw new MapGeneratorException("Exit door not placed correctly");

        // Place key in far away room
        var keyRoom = GetFarthestRoomFromTwo(map.Player1.Position, infrontDoorPos);
        var keyPosition = keyRoom.RandomPosition(1);
        map[keyPosition] = ToKey(keyColor);
    }

    private void GenerateLevel4()
    {
        CreateRandomRooms(30, 3, 15, 1);
        ConnectRoomsRandomly();
        PlacePlayerTopLeftAndExitBottomRight();

        // Add exit door
        var (exitKeyColor, exitDoorPos) = AddLockAroundExit();
        var infrontExitDoorPos = GetEmptyPositionInFront(exitDoorPos) ?? throw new MapGeneratorException("Exit door not placed correctly");

        // Find room farthest away from player and exit large enough for locker room.
        var lockerRoom = GetFarthestRoomFromTwo(map.Player1.Position, infrontExitDoorPos, 5, 5);
        availableRooms = availableRooms.Remove(lockerRoom);
        var (lockerKeyColor, infrontLockerDoorPos) = AddLockerToRoom(lockerRoom, exitKeyColor);

        // Place key farthest away from player and locker room
        var lockerKeyRoom = GetFarthestRoomFromTwo(map.Player1.Position, infrontLockerDoorPos);
        availableRooms = availableRooms.Remove(lockerKeyRoom);
        var lockerKeyPos = lockerKeyRoom.RandomPosition(1);
        map[lockerKeyPos] = ToKey(lockerKeyColor);
    }

    private void GenerateLevel5()
    {
        CreateRandomRooms(30, 3, 15, 1);
        ConnectRoomsRandomly();
        PlacePlayerTopLeftAndExitBottomRight();

        // Add exit door
        var (exitKeyColor, exitDoorPos) = AddLockAroundExit();
        var infrontExitDoorPos = GetEmptyPositionInFront(exitDoorPos) ?? throw new MapGeneratorException("Exit door not placed correctly");

        // Find room farthest away from player and exit large enough for locker room.
        var locker1Room = GetFarthestRoomFromTwo(map.Player1.Position, infrontExitDoorPos, 5, 5);
        availableRooms = availableRooms.Remove(locker1Room);
        var (locker1KeyColor, infrontLocker1DoorPos) = AddLockerToRoom(locker1Room, exitKeyColor);

        // Add another locker
        var locker2Room = GetFarthestRoomFromTwo(infrontExitDoorPos, infrontLocker1DoorPos, 5, 5);
        availableRooms = availableRooms.Remove(locker2Room);
        var (locker2KeyColor, infrontLocker2DoorPos) = AddLockerToRoom(locker2Room, locker1KeyColor);

        // Place key farthest away from player and locker room
        var locker2KeyRoom = GetFarthestRoomFromTwo(infrontLocker1DoorPos, infrontLocker2DoorPos);
        availableRooms = availableRooms.Remove(locker2KeyRoom);
        var locker2KeyPos = locker2KeyRoom.RandomPosition(1);
        map[locker2KeyPos] = ToKey(locker2KeyColor);
    }

    private void GenerateLevel6()
    {
        // Double-door locker room.
        // Two doors to enter room with exit key.
        // Both keys are in the open.
        // Key for inner door is close to the player at startup, so it can accidentally pick it up.
        // Outer door key is far away from room and player.

        // Need at least one big room where double locker can fit.
        // Inner locker is 3x3, outer locker is 7x7, so large room must be at least 9x9
        var lockerRoom = CreateRandomRoom(9, 15, 1, 5, height - 5, 5, width - 5);
        Debug.Assert(lockerRoom != null);
        // Create extra rooms to fill map
        CreateRandomRooms(30, 3, 10, 2);
        // Connect them all
        ConnectRoomsRandomly();
        // Place player and exit
        PlacePlayerTopLeftAndExitBottomRight();
        var (exitKeyColor, exitDoor) = AddLockAroundExit();

        // Create double locker room
        var (cy, cx) = lockerRoom.Center;

        map[cy, cx] = ToKey(exitKeyColor);
        for (int y = cy - 1; y <= cy + 1; y++)
        {
            for (int x = cx - 1; x <= cx + 1; x++)
            {
                if (map[y, x] == Cell.Empty)
                {
                    map[y, x] = Cell.Wall;
                }
            }
        }

        for (int x = cx - 3; x <= cx + 3; x++)
        {
            if (map[cy - 3, x] == Cell.Empty)
            {
                map[cy - 3, x] = Cell.Wall;
            }
            if (map[cy + 3, x] == Cell.Empty)
            {
                map[cy + 3, x] = Cell.Wall;
            }
        }
        for (int y = cy - 2; y <= cy + 2; y++)
        {
            if (map[y, cx - 3] == Cell.Empty)
            {
                map[y, cx - 3] = Cell.Wall;
            }
            if (map[y, cx + 3] == Cell.Empty)
            {
                map[y, cx + 3] = Cell.Wall;
            }
        }

        var innerColor = availableKeyColors.PickOne();
        availableKeyColors = availableKeyColors.Remove(innerColor);
        map[cy - 1, cx] = ToDoor(innerColor);

        var outerColor = availableKeyColors.PickOne();
        availableKeyColors = availableKeyColors.Remove(outerColor);
        map[cy + 3, cx] = ToDoor(outerColor);


        // Place inner key in room closest to player so it can accidentally be picked up
        var innerKeyRoom = GetClosestRoomFrom(availableRooms, map.Player1.Position);
        availableRooms = availableRooms.Remove(innerKeyRoom);
        var innerKeyPos = innerKeyRoom.RandomPosition(1);
        map[innerKeyPos] = ToKey(innerColor);

        // Place outer key far from exit and player
        var outerKeyRoom = GetFarthestRoomFromTwo(map.Player1.Position, exitDoor);
        availableRooms = availableRooms.Remove(outerKeyRoom);
        var outerKeyPos = outerKeyRoom.RandomPosition(1);
        map[outerKeyPos] = ToKey(outerColor);
    }

    private void GenerateLevel7()
    {
        // First combat.
        // Key in left side for door to enter right side.
        // One sword and armor in left side.
        // One enemy in right side.
        // Open exit in right side, so evading can also be a strategy.


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

        // Add player and exit
        PlacePlayerTopLeftAndExitBottomRight();
        var (exitKeyColor, _) = AddLockAroundExit();

        // Connect left and right rooms closest to each other
        var minDist = double.PositiveInfinity;
        Room? minLeft = null;
        Room? minRight = null;
        foreach (var left in roomsLeft)
        {
            foreach (var right in roomsRight)
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

        // Create door in tunnel between left and right
        var keyColor = availableKeyColors.PickOne();
        availableKeyColors = availableKeyColors.Remove(keyColor);

        var doorPositions = ImmutableList<int>.Empty;
        for (var y = 0; y < height; y++)
        {
            if (map[y, middle] == Cell.Empty)
            {
                map[y, middle] = ToDoor(keyColor);
                doorPositions = doorPositions.Add(y);
            }
        }

        // Place key left, far from player
        Position farthestInfrontDoor = (0, 0);
        {
            var maxDist = 0;
            var (distances, _) = ComputeDistancesFrom(map.Player1.Position);
            foreach (var doorPos in doorPositions)
            {
                var doorLeft = (doorPos, middle - 1);
                if (distances.TryGetValue(doorLeft, out var dist))
                {
                    if (dist > maxDist)
                    {
                        maxDist = dist;
                        farthestInfrontDoor = doorLeft;
                    }
                }
            }
        }

        var keyRoom = GetFarthestRoomFromTwo(map.Player1.Position, farthestInfrontDoor);
        var keyPos = keyRoom.RandomPosition(1);
        map[keyPos] = ToKey(keyColor);

        // Place sword in any room on the left
        var swordRoom = roomsLeft.Where(r => availableRooms.Contains(r)).PickOne();
        availableRooms = availableRooms.Remove(swordRoom);
        var swordPos = swordRoom.RandomPosition(0);
        map[swordPos] = Cell.Sword;

        // Place health in any room on the left
        var healthRoom = roomsLeft.Where(r => availableRooms.Contains(r)).PickOne();
        availableRooms = availableRooms.Remove(healthRoom);
        var healthPos = healthRoom.RandomPosition(0);
        map[healthPos] = Cell.Health;

        // Place enemy in any room on the right with key to exit
        var enemyRoom = roomsRight.Where(r => availableRooms.Contains(r)).PickOne();
        var enemyPos = enemyRoom.RandomPosition(0);
        map.Enemy1.Position = enemyPos;
        map.Enemy1.Inventory = ToInventory(exitKeyColor);
    }

    private void GenerateLevel8()
    {
        // Prison
        // One big room that holds the second player.
        // Guard has key to prison.
        // Exit is open.
        // Health and sword spread around map.

        // Need at least one big room that can fit prison
        var prisonRoom = CreateRandomRoom(11, 15, 0, 5, height - 5, 5, width - 5);
        Debug.Assert(prisonRoom != null);
        // Create extra rooms to fill map
        CreateRandomRooms(30, 3, 10, 2);
        // Connect them all
        ConnectRoomsRandomly();

        availableRooms = availableRooms.Remove(prisonRoom);

        // Place player and exit
        PlacePlayerTopLeftAndExitBottomRight();

        // Create prison
        var prisonKeyColor = availableKeyColors.PickOne();
        availableKeyColors = availableKeyColors.Remove(prisonKeyColor);

        var (cy, cx) = prisonRoom.Center;
        for (int x = cx - 3; x <= cx + 3; x++)
        {
            if (map[cy - 3, x] == Cell.Empty)
            {
                map[cy - 3, x] = Cell.Wall;
            }
            if (map[cy + 3, x] == Cell.Empty)
            {
                map[cy + 3, x] = Cell.Wall;
            }
        }
        for (int y = cy - 2; y <= cy + 2; y++)
        {
            if (map[y, cx - 3] == Cell.Empty)
            {
                map[y, cx - 3] = Cell.Wall;
            }
            if (map[y, cx + 3] == Cell.Empty)
            {
                map[y, cx + 3] = Cell.Wall;
            }
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
        var swordRoom = GetFarthestRoomFromTwo(map.Player1.Position, map.Enemy1.Position);
        availableRooms = availableRooms.Remove(swordRoom);
        var swordPos = swordRoom.RandomPosition(1);
        map[swordPos] = Cell.Sword;

        // Place health randomly
        var healthRoom = GetFarthestRoomFromTwo(map.Player1.Position, swordPos);
        availableRooms = availableRooms.Remove(healthRoom);
        var healthPos = healthRoom.RandomPosition(1);
        map[healthPos] = Cell.Health;
    }

    private void GenerateLevel9()
    {
        CreateSplitWorldWithPlayersLeftAndExitRight(out var middle, out var roomsLeft, out var roomsRight);

        var (exitKeyColor, _) = AddLockAroundExit();

        // Create door in tunnel between left and right
        var keyColor = availableKeyColors.PickOne();
        availableKeyColors = availableKeyColors.Remove(keyColor);
        var doorPositions = ConnectLeftAndRightWithDoor(middle, roomsLeft, roomsRight, ToDoor(keyColor));

        // Place key left, far from player
        Position farthestInfrontDoor = (0, 0);
        {
            var maxDist = 0;
            var (distances, _) = ComputeDistancesFrom(map.Player1.Position);
            foreach (var doorPos in doorPositions)
            {
                var doorLeft = (doorPos, middle - 1);
                if (distances.TryGetValue(doorLeft, out var dist))
                {
                    if (dist > maxDist)
                    {
                        maxDist = dist;
                        farthestInfrontDoor = doorLeft;
                    }
                }
            }
        }

        var keyRoom = GetFarthestRoomFromTwo(map.Player1.Position, farthestInfrontDoor);
        var keyPos = keyRoom.RandomPosition(1);
        map[keyPos] = ToKey(keyColor);

        // Place sword in any room on the left
        var swordRoom1 = roomsLeft.Where(r => availableRooms.Contains(r)).PickOne();
        availableRooms = availableRooms.Remove(swordRoom1);
        var swordPos1 = swordRoom1.RandomPosition(0);
        map[swordPos1] = Cell.Sword;

        // Place another sword in any room on the left
        var swordRoom2 = roomsLeft.Where(r => availableRooms.Contains(r)).PickOne();
        availableRooms = availableRooms.Remove(swordRoom2);
        var swordPos2 = swordRoom2.RandomPosition(0);
        map[swordPos2] = Cell.Sword;

        // Place enemy in any room on the right with key to exit
        var enemyRoom = roomsRight.Where(r => availableRooms.Contains(r)).PickOne();
        availableRooms = availableRooms.Remove(enemyRoom);
        var enemyPos = enemyRoom.RandomPosition(0);
        map.Enemy1.Position = enemyPos;
        map.Enemy1.Inventory = ToInventory(exitKeyColor);
    }

    private void GenerateLevel10()
    {
        CreateSplitWorldWithPlayersLeftAndExitRight(out var middle, out var roomsLeft, out var roomsRight);

        var (exitKeyColor, _) = AddLockAroundExit();

        var doorPositionsBlack = ConnectLeftAndRightWithDoor(middle, roomsLeft, roomsRight, Cell.DoorBlackClosed);

        // Place pressure plate left, far from player
        Position farthestInfrontDoor = (0, 0);
        {
            var maxDist = 0;
            var (distances, _) = ComputeDistancesFrom(map.Player1.Position);
            foreach (var doorPos in doorPositionsBlack)
            {
                var doorLeft = (doorPos, middle - 1);
                if (distances.TryGetValue(doorLeft, out var dist))
                {
                    if (dist > maxDist)
                    {
                        maxDist = dist;
                        farthestInfrontDoor = doorLeft;
                    }
                }
            }
        }

        var plateRoom = GetFarthestRoomFromTwo(map.Player1.Position, farthestInfrontDoor);
        var platePos = plateRoom.RandomPosition(1);
        map[platePos] = Cell.PressurePlate;


        // Create another tunnel with a door
        var doorKeyColor = availableKeyColors.PickOne();
        availableKeyColors = availableKeyColors.Remove(doorKeyColor);
        var doorPositionsColored = ConnectLeftAndRightWithDoor(middle, roomsLeft, roomsRight, ToDoor(doorKeyColor));

        // Place key in a room on the right farthest from black doors
        Position? infrontDoor = null;
        {
            foreach (var doorPos in doorPositionsBlack)
            {
                Position doorRight = (doorPos, middle + 1);
                if (map[doorRight] == Cell.Empty)
                {
                    infrontDoor = doorRight;
                    break;
                }
            }
        }
        Debug.Assert(infrontDoor != null);

        var keyRoom = GetFarthestRoomFrom(roomsRight.Where(r => availableRooms.Contains(r)), infrontDoor.Value);
        availableRooms = availableRooms.Remove(keyRoom);
        var keyPos = keyRoom.RandomPosition(0);
        map[keyPos] = ToKey(doorKeyColor);

        // Put exit key on random room in the left
        var exitKeyRoom = roomsLeft.Where(r => availableRooms.Contains(r)).PickOne();
        availableRooms = availableRooms.Remove(exitKeyRoom);
        var exitKeyPos = exitKeyRoom.RandomPosition(1);
        map[exitKeyPos] = ToKey(exitKeyColor);
    }

    private void GenerateLevel11()
    {
        CreateRandomRooms(50, 3, 12, 2);
        ConnectRoomsRandomly();
        PlaceTwoPlayersTopLeftAndExitBottomRight();
        var (exitKeyColor, exitDoor) = AddLockAroundExit();

        var exitRoom = FindRoomContainingPosition(exitPosition) ?? throw new MapGeneratorException("Exit room not found");

        var availablePositions = ImmutableHashSet<Position>.Empty;
        for (var y = exitRoom.Top; y < exitRoom.Bottom; y++)
        {
            for (var x = exitRoom.Left; x < exitRoom.Right; x++)
            {
                var pos = (y, x);
                if (map[pos] == Cell.Empty) availablePositions = availablePositions.Add(pos);
            }
        }

        var enemy1Pos = availablePositions.PickOne();
        availablePositions = availablePositions.Remove(enemy1Pos);

        var enemy2Pos = availablePositions.PickOne();
        //availablePositions = availablePositions.Remove(enemy2Pos);

        map.Enemy1.Position = enemy1Pos;
        map.Enemy2.Position = enemy2Pos;
        map.Enemy2.Inventory = ToInventory(exitKeyColor);

        var health1Pos = GetFarthestPositionFrom(map.Player1.Position, map.Enemy1.Position);
        map[health1Pos] = Cell.Health;

        var health2Pos = GetFarthestPositionFrom(map.Player2.Position, map.Enemy2.Position, health1Pos);
        map[health2Pos] = Cell.Health;

        var sword1Pos = GetFarthestPositionFrom(map.Player2.Position, map.Enemy2.Position, health1Pos, health2Pos);
        map[sword1Pos] = Cell.Sword;
        
        var sword2Pos = GetFarthestPositionFrom(map.Player2.Position, map.Enemy2.Position, health1Pos, health2Pos, sword1Pos);
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
        var rw = random.Next(minSize, maxSize);
        var rh = random.Next(minSize, maxSize);

        var choices = allPosition.Where(p => minY <= p.y && p.y < maxY && minX <= p.x && p.x < maxX).ToImmutableHashSet();

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
            var (ry, rx) = choices.OrderBy(_ => random.Next()).First();
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
            var next = closestRooms.OrderBy(_ => random.Next()).First();

            ConnectRooms(current, next);
            current = next;
        }
    }

    private void PlacePlayerTopLeftAndExitBottomRight()
    {
        // place player in top left room
        // and exit in bottom right
        var playerRoom = availableRooms.OrderBy(r => r.Center.DistanceTo((0, 0))).First();
        availableRooms = availableRooms.Remove(playerRoom);

        var exitRoom = availableRooms.OrderBy(r => r.Center.DistanceTo((height, width))).First();
        availableRooms = availableRooms.Remove(exitRoom);

        map.Player1.Position = playerRoom.Center;
        exitPosition = (exitRoom.Bottom - 1, exitRoom.Right - 1);
        map[exitPosition] = Cell.Exit;
    }

    private void PlaceTwoPlayersTopLeftAndExitBottomRight()
    {
        var playerRoom = availableRooms.OrderBy(r => r.Center.DistanceTo((0, 0))).First();
        availableRooms = availableRooms.Remove(playerRoom);

        var exitRoom = availableRooms.OrderBy(r => r.Center.DistanceTo((height, width))).First();
        availableRooms = availableRooms.Remove(exitRoom);

        map.Player1.Position = (playerRoom.Center.y + 1, playerRoom.Center.x - 1);
        map.Player2.Position = (playerRoom.Center.y - 1, playerRoom.Center.x + 1);
        exitPosition = (exitRoom.Bottom - 1, exitRoom.Right - 1);
        map[exitPosition] = Cell.Exit;
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
        var keyColor = availableKeyColors.PickOne();
        availableKeyColors = availableKeyColors.Remove(keyColor);

        // Place wall of doors around exit
        Position doorPos = new();
        for (var y = exitPosition.y - 1; y <= exitPosition.y + 1; y++)
        {
            for (var x = exitPosition.x - 1; x <= exitPosition.x + 1; x++)
            {
                if (0 <= y && y < height && 0 <= x && x < width)
                {
                    if (map[y, x] == Cell.Empty)
                    {
                        map[y, x] = ToDoor(keyColor);
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

    private static double StdDev(params double[] values) => StdDev(values.AsEnumerable());

    private static double StdDev(IEnumerable<double> values)
    {
        var mean = values.Average();
        var stdDev2 = values.Select(v => v - mean).Select(v => v * v).Average();
        return Math.Sqrt(stdDev2);
    }

    private Position GetFarthestPositionFrom(params Position[] inputPositions)
    {
        var inputDistances = ImmutableList<IImmutableDictionary<Position, int>>.Empty;
        foreach (var pos in inputPositions)
        {
            var (distances, _) = ComputeDistancesFrom(pos);
            inputDistances = inputDistances.Add(distances);
        }
        var checkPositions = inputDistances[0].Keys;

        double minSpread = double.PositiveInfinity;
        var minPositions = ImmutableHashSet<Position>.Empty;
        foreach (var pos in checkPositions)
        {
            var posDistances = inputDistances.Where(d => d.ContainsKey(pos)).Select(d=> (double)d[pos]).ToImmutableArray();
            if (posDistances.Length != inputDistances.Count) continue;

            var stdDev = StdDev(posDistances);
            if (stdDev < minSpread)
            {
                minSpread = stdDev;
                minPositions = [pos];
            }
            else if (stdDev == minSpread)
            {
                minPositions = minPositions.Add(pos);
            }
        }

        return minPositions.PickOne();
    }


    private Room GetClosestRoomFrom(IEnumerable<Room> rooms, Position pos, int minRoomHeight = 1, int minRoomWidth = 1)
    {
        var (distances, _) = ComputeDistancesFrom(pos);

        int minDistance = int.MaxValue;
        var minRooms = ImmutableHashSet<Room>.Empty;

        foreach (var room in rooms.Where(r => r.Height >= minRoomHeight && r.Width >= minRoomWidth))
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

        return minRooms.PickOne();
    }

    private Room GetFarthestRoomFrom(IEnumerable<Room> rooms, Position pos, int minRoomHeight = 1, int minRoomWidth = 1)
    {
        var (distances, _) = ComputeDistancesFrom(pos);

        int? maxDistance = null;
        var maxRooms = ImmutableHashSet<Room>.Empty;

        foreach (var room in rooms.Where(r => r.Height >= minRoomHeight && r.Width >= minRoomWidth))
        {
            if (distances.TryGetValue(room.Center, out var dist))
            {
                if (maxDistance == null || dist > maxDistance)
                {
                    maxDistance = dist;
                    maxRooms = [room];
                }
                else if (maxDistance != null && dist == maxDistance)
                {
                    maxRooms = maxRooms.Add(room);
                }
            }
        }

        return maxRooms.PickOne();
    }

    private Room GetFarthestRoomFromTwo(Position a, Position b, int minRoomHeight = 1, int minRoomWidth = 1)
    {
        var (distancesA, _) = ComputeDistancesFrom(a);
        var (distancesB, _) = ComputeDistancesFrom(b);

        int? maxDistance = null;
        var maxRooms = ImmutableHashSet<Room>.Empty;

        foreach (var room in availableRooms.Where(r => r.Height >= minRoomHeight && r.Width >= minRoomWidth))
        {
            if (distancesA.TryGetValue(room.Center, out var distA) &&
                distancesB.TryGetValue(room.Center, out var distB))
            {
                var dist = distA + distB;
                if (maxDistance == null || dist > maxDistance)
                {
                    maxDistance = dist;
                    maxRooms = [room];
                }
                else if (maxDistance != null && dist == maxDistance)
                {
                    maxRooms = maxRooms.Add(room);
                }
            }
        }

        return maxRooms.PickOne();
    }

    private Position? GetEmptyPositionInFront(Position pos)
    {
        if (pos.y > 0 && map[pos.y - 1, pos.x] == Cell.Empty) return (pos.y - 1, pos.x);
        if (pos.y < height && map[pos.y + 1, pos.x] == Cell.Empty) return (pos.y + 1, pos.x);
        if (pos.x > 0 && map[pos.y, pos.x - 1] == Cell.Empty) return (pos.y, pos.x - 1);
        if (pos.x < width && map[pos.y, pos.x + 1] == Cell.Empty) return (pos.y, pos.x + 1);

        return null;
    }

    private Room? FindRoomContainingPosition(Position pos)
    {
        foreach (var room in rooms)
        {
            if (room.Top <= pos.y && pos.y < room.Bottom &&
                room.Left <= pos.x && pos.x < room.Right)
            {
                return room;
            }
        }
        return null;
    }

    private (KeyColor lockerKeyColor, Position infrontDoorPos) AddLockerToRoom(Room lockerRoom, KeyColor keyColor)
    {
        // Create locker room (3x3) in random position in room
        var lockerCenter = lockerRoom.RandomPosition(2);
        map[lockerCenter] = ToKey(keyColor);
        for (var y = lockerCenter.y - 1; y <= lockerCenter.y + 1; y++)
        {
            for (var x = lockerCenter.x - 1; x <= lockerCenter.x + 1; x++)
            {
                if (map[y, x] == Cell.Empty)
                {
                    map[y, x] = Cell.Wall;
                }
            }
        }

        // Add a door to the locker room
        var lockerDoorPos = PickRandomDoorPosForSmallRoom(lockerCenter);
        var lockerKeyColor = availableKeyColors.PickOne();
        availableKeyColors = availableKeyColors.Remove(lockerKeyColor);
        map[lockerDoorPos] = ToDoor(lockerKeyColor);
        var infrontLockerDoorPos = GetEmptyPositionInFront(lockerDoorPos) ?? throw new MapGeneratorException("Locker door not placed correctly");
        Debug.Assert(map[infrontLockerDoorPos] == Cell.Empty);
        return (lockerKeyColor, infrontLockerDoorPos);
    }

    private void CreateSplitWorldWithPlayersLeftAndExitRight(out int middle, out IImmutableList<Room> roomsLeft, out IImmutableList<Room> roomsRight)
    {
        // Create left and right rooms
        middle = width / 2;
        rooms = [];
        CreateRandomRooms(15, 3, 12, 2, maxX: middle);
        ConnectRoomsRandomly();
        roomsLeft = rooms;
        rooms = [];
        CreateRandomRooms(15, 3, 12, 2, minX: middle + 1);
        ConnectRoomsRandomly();
        roomsRight = rooms;
        rooms = roomsLeft.AddRange(roomsRight);

        // Add players and exit
        var playerRoom = roomsLeft.Where(r => availableRooms.Contains(r)).Where(r => r.Width >= 3 && r.Height >= 3).OrderBy(r => r.Center.DistanceTo((0, 0))).First();
        var exitRoom = roomsRight.Where(r => availableRooms.Contains(r)).OrderBy(r => r.Center.DistanceTo((0, 0))).Last();

        map.Player1.Position = (playerRoom.Top, playerRoom.Left);
        map.Player2.Position = (playerRoom.Bottom - 1, playerRoom.Right - 1);

        exitPosition = (exitRoom.Bottom - 1, exitRoom.Right - 1);
        map[exitPosition] = Cell.Exit;
        availableRooms = availableRooms.Remove(playerRoom).Remove(exitRoom);
    }

    private ImmutableList<int> ConnectLeftAndRightWithDoor(int middle, IImmutableList<Room> roomsLeft, IImmutableList<Room> roomsRight, Cell door)
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

        // Create pressure plate door in tunnel between left and right
        var doorPositions = ImmutableList<int>.Empty;
        var top = Math.Min(minLeft.Top, minRight.Top);
        var bottom = Math.Max(minLeft.Bottom, minRight.Bottom);
        for (var y = top; y < bottom; y++)
        {
            if (map[y, middle] == Cell.Empty)
            {
                map[y, middle] = door;
                doorPositions = doorPositions.Add(y);
            }
        }

        return doorPositions;
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
}