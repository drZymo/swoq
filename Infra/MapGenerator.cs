namespace Swoq.Infra;

using System.Collections.Immutable;
using System.Diagnostics;
using Position = (int y, int x);

public class MapGenerator
{
    private class MapGeneratorException(string message) : Exception(message) { }

    private static readonly Random random = new();

    private readonly int height;
    private readonly int width;
    private readonly Cell[,] data;
    private Position initialPlayer1Position;
    private Position? initialPlayer2Position = null;
    private Position? initialEnemy1Position = null;
    private Inventory initialEnemy1Inventory = Inventory.None;
    private Position? initialEnemy2Position = null;
    private Inventory initialEnemy2Inventory = Inventory.None;
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
        data = new Cell[height, width];

        // Fill with walls
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                data[y, x] = Cell.Wall;
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

        var data = this.data.Cast<Cell>().ToImmutableArray();
        return new Map(data, height, width,
            initialPlayer1Position,
            initialPlayer2Position,
            initialEnemy1Position, initialEnemy1Inventory,
            initialEnemy2Position, initialEnemy2Inventory);
    }

    private void GenerateLevel0()
    {
        // TODO: subclasses per level?
        CreateRoom(32, 32, 10, 10);
        initialPlayer1Position = (32 - 5, 32 - 5);
        data[32 + 4, 32 + 5] = Cell.Exit;
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
        var keyRoom = GetFarthestRoomFromTwo(initialPlayer1Position, infrontDoorPos);
        var keyPosition = keyRoom.RandomPosition(1);
        data[keyPosition.y, keyPosition.x] = ToKey(keyColor);
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
        var lockerRoom = GetFarthestRoomFromTwo(initialPlayer1Position, infrontExitDoorPos, 5, 5);
        availableRooms = availableRooms.Remove(lockerRoom);
        var (lockerKeyColor, infrontLockerDoorPos) = AddLockerToRoom(lockerRoom, exitKeyColor);

        // Place key farthest away from player and locker room
        var lockerKeyRoom = GetFarthestRoomFromTwo(initialPlayer1Position, infrontLockerDoorPos);
        availableRooms = availableRooms.Remove(lockerKeyRoom);
        var lockerKeyPos = lockerKeyRoom.RandomPosition(1);
        data[lockerKeyPos.y, lockerKeyPos.x] = ToKey(lockerKeyColor);
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
        var locker1Room = GetFarthestRoomFromTwo(initialPlayer1Position, infrontExitDoorPos, 5, 5);
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
        data[locker2KeyPos.y, locker2KeyPos.x] = ToKey(locker2KeyColor);
    }

    private void GenerateLevel6()
    {
        var room1 = CreateRandomRoom(5, 10, 1, 0, height, 0, width / 2);
        Debug.Assert(room1 != null);
        var room2 = CreateRandomRoom(5, 10, 1, 0, height, width / 2, width);
        Debug.Assert(room2 != null);
        Debug.Assert(rooms.Count == 2);
        ConnectRooms(room1, room2);

        PlacePlayerTopLeftAndExitBottomRight();

        var (exitKeyColor, _) = AddLockAroundExit();

        initialEnemy1Position = room2.Center;
        initialEnemy1Inventory = ToInventory(exitKeyColor);

        Position swordPos = (room1.Top + 1, room1.Left + 1);
        data[swordPos.y, swordPos.x] = Cell.Sword;

        Position healthPos = (room1.Top + 1, room1.Right - 2);
        data[healthPos.y, healthPos.x] = Cell.Health;

        availableRooms = availableRooms.Remove(room1).Remove(room2);
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
            if (data[y, middle] == Cell.Empty)
            {
                data[y, middle] = ToDoor(keyColor);
                doorPositions = doorPositions.Add(y);
            }
        }

        // Place key left, far from player
        Position farthestInfrontDoor = (0, 0);
        {
            var maxDist = 0;
            var (distances, _) = ComputeDistancesFrom(initialPlayer1Position);
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

        var keyRoom = GetFarthestRoomFromTwo(initialPlayer1Position, farthestInfrontDoor);
        var keyPos = keyRoom.RandomPosition(1);
        data[keyPos.y, keyPos.x] = ToKey(keyColor);

        // Place sword in any room on the left
        var swordRoom = roomsLeft.Where(r => availableRooms.Contains(r)).PickOne();
        availableRooms = availableRooms.Remove(swordRoom);
        var swordPos = swordRoom.RandomPosition(0);
        data[swordPos.y, swordPos.x] = Cell.Sword;

        // Place health in any room on the left
        var healthRoom = roomsLeft.Where(r => availableRooms.Contains(r)).PickOne();
        availableRooms = availableRooms.Remove(healthRoom);
        var healthPos = healthRoom.RandomPosition(0);
        data[healthPos.y, healthPos.x] = Cell.Health;

        // Place enemy in any room on the right with key to exit
        var enemyRoom = roomsRight.Where(r => availableRooms.Contains(r)).PickOne();
        var enemyPos = enemyRoom.RandomPosition(0);
        initialEnemy1Position = enemyPos;
        initialEnemy1Inventory = ToInventory(exitKeyColor);
    }

    private void GenerateLevel8()
    {
        // Two player exit
        var room = CreateRoom(10, 10, 8, 16);

        initialPlayer1Position = (room.Top + 1, room.Left + 1);
        initialPlayer2Position = (room.Top + 3, room.Left + 1);

        exitPosition = (room.Bottom - 1, room.Right);
        data[exitPosition.y, exitPosition.x] = Cell.Exit;
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
            var (distances, _) = ComputeDistancesFrom(initialPlayer1Position);
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

        var keyRoom = GetFarthestRoomFromTwo(initialPlayer1Position, farthestInfrontDoor);
        var keyPos = keyRoom.RandomPosition(1);
        data[keyPos.y, keyPos.x] = ToKey(keyColor);

        // Place sword in any room on the left
        var swordRoom1 = roomsLeft.Where(r => availableRooms.Contains(r)).PickOne();
        availableRooms = availableRooms.Remove(swordRoom1);
        var swordPos1 = swordRoom1.RandomPosition(0);
        data[swordPos1.y, swordPos1.x] = Cell.Sword;

        // Place another sword in any room on the left
        var swordRoom2 = roomsLeft.Where(r => availableRooms.Contains(r)).PickOne();
        availableRooms = availableRooms.Remove(swordRoom2);
        var swordPos2 = swordRoom2.RandomPosition(0);
        data[swordPos2.y, swordPos2.x] = Cell.Sword;

        // Place enemy in any room on the right with key to exit
        var enemyRoom = roomsRight.Where(r => availableRooms.Contains(r)).PickOne();
        availableRooms = availableRooms.Remove(enemyRoom);
        var enemyPos = enemyRoom.RandomPosition(0);
        initialEnemy1Position = enemyPos;
        initialEnemy1Inventory = ToInventory(exitKeyColor);
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
            var (distances, _) = ComputeDistancesFrom(initialPlayer1Position);
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

        var plateRoom = GetFarthestRoomFromTwo(initialPlayer1Position, farthestInfrontDoor);
        var platePos = plateRoom.RandomPosition(1);
        data[platePos.y, platePos.x] = Cell.PressurePlate;


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
                if (data[doorRight.y, doorRight.x] == Cell.Empty)
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
        data[keyPos.y, keyPos.x] = ToKey(doorKeyColor);

        // Put exit key on random room in the left
        var exitKeyRoom = roomsLeft.Where(r => availableRooms.Contains(r)).PickOne();
        availableRooms = availableRooms.Remove(exitKeyRoom);
        var exitKeyPos = exitKeyRoom.RandomPosition(1);
        data[exitKeyPos.y, exitKeyPos.x] = ToKey(exitKeyColor);
    }

    private Room CreateRoom(int y, int x, int height, int width)
    {
        var room = new Room(y, x, height, width);

        // Make empty
        for (var my = room.Top; my < room.Bottom; my++)
            for (var mx = room.Left; mx < room.Right; mx++)
                data[my, mx] = Cell.Empty;

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
        var roomsOrdered = availableRooms.OrderBy(r => r.Center.DistanceTo((0, 0)));
        var playerRoom = roomsOrdered.First();
        var exitRoom = roomsOrdered.Last();

        initialPlayer1Position = playerRoom.Center;
        exitPosition = (exitRoom.Bottom - 1, exitRoom.Right);
        data[exitPosition.y, exitPosition.x] = Cell.Exit;

        availableRooms = availableRooms.Remove(playerRoom).Remove(exitRoom);
    }


    private void DrawHLine(int y, int x1, int x2, int dir, Cell value = Cell.Empty)
    {
        if (x1 == x2) return;
        var start = Math.Min(x1, x2);
        var end = Math.Max(x1, x2);
        for (var x = start; x <= end; x++)
        {
            data[y, x] = value;
            data[y - dir, x] = value;
        }
    }

    private void DrawVLine(int y1, int y2, int x, int dir, Cell value = Cell.Empty)
    {
        if (y1 == y2) return;
        var start = Math.Min(y1, y2);
        var end = Math.Max(y1, y2);
        for (var y = start; y <= end; y++)
        {
            data[y, x] = value;
            data[y, x - dir] = value;
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
        for (var y = exitPosition.y - 1; y <= exitPosition.y + 1; y++)
        {
            for (var x = exitPosition.x - 1; x <= exitPosition.x + 1; x++)
            {
                if (0 <= y && y < height && 0 <= x && x < width)
                {
                    if (data[y, x] == Cell.Empty)
                    {
                        data[y, x] = Cell.Wall;
                    }
                }
            }
        }

        var doorPos = PickRandomDoorPosForSmallRoom(exitPosition);

        // pick a random key
        var key = availableKeyColors.PickOne();
        availableKeyColors = availableKeyColors.Remove(key);

        data[doorPos.y, doorPos.x] = ToDoor(key);

        return (key, doorPos);
    }

    private Position PickRandomDoorPosForSmallRoom(Position roomCenter)
    {
        var posibleDoorPositions = new List<Position>();
        if (roomCenter.y < height - 2 && data[roomCenter.y + 2, roomCenter.x] == Cell.Empty)
        {
            posibleDoorPositions.Add((roomCenter.y + 1, roomCenter.x));
        }
        if (roomCenter.y > 2 && data[roomCenter.y - 2, roomCenter.x] == Cell.Empty)
        {
            posibleDoorPositions.Add((roomCenter.y - 1, roomCenter.x));
        }
        if (roomCenter.x < width - 2 && data[roomCenter.y, roomCenter.x + 2] == Cell.Empty)
        {
            posibleDoorPositions.Add((roomCenter.y, roomCenter.x + 1));
        }
        if (roomCenter.x > 2 && data[roomCenter.y, roomCenter.x - 2] == Cell.Empty)
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
            if (!data[nextPos.y, nextPos.x].CanWalkOn()) return;

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

    private Position GetFarthestPositionFromTwo(Position a, Position b)
    {
        var (distancesA, _) = ComputeDistancesFrom(a);
        var (distancesB, _) = ComputeDistancesFrom(b);

        int? maxDistance = null;
        var maxPositions = ImmutableHashSet<Position>.Empty;
        foreach (var (pos, distA) in distancesA)
        {
            if (distancesB.TryGetValue(pos, out var distB))
            {
                var dist = distA + distB;
                if (maxDistance == null || dist > maxDistance)
                {
                    maxDistance = dist;
                    maxPositions = [pos];
                }
                else if (maxDistance != null && dist == maxDistance)
                {
                    maxPositions = maxPositions.Add(pos);
                }
            }
        }

        return maxPositions.PickOne();
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
        if (pos.y > 0 && data[pos.y - 1, pos.x] == Cell.Empty) return (pos.y - 1, pos.x);
        if (pos.y < height && data[pos.y + 1, pos.x] == Cell.Empty) return (pos.y + 1, pos.x);
        if (pos.x > 0 && data[pos.y, pos.x - 1] == Cell.Empty) return (pos.y, pos.x - 1);
        if (pos.x < width && data[pos.y, pos.x + 1] == Cell.Empty) return (pos.y, pos.x + 1);

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
        data[lockerCenter.y, lockerCenter.x] = ToKey(keyColor);
        for (var y = lockerCenter.y - 1; y <= lockerCenter.y + 1; y++)
        {
            for (var x = lockerCenter.x - 1; x <= lockerCenter.x + 1; x++)
            {
                if (data[y, x] == Cell.Empty)
                {
                    data[y, x] = Cell.Wall;
                }
            }
        }

        // Add a door to the locker room
        var lockerDoorPos = PickRandomDoorPosForSmallRoom(lockerCenter);
        var lockerKeyColor = availableKeyColors.PickOne();
        availableKeyColors = availableKeyColors.Remove(lockerKeyColor);
        data[lockerDoorPos.y, lockerDoorPos.x] = ToDoor(lockerKeyColor);
        var infrontLockerDoorPos = GetEmptyPositionInFront(lockerDoorPos) ?? throw new MapGeneratorException("Locker door not placed correctly");
        Debug.Assert(data[infrontLockerDoorPos.y, infrontLockerDoorPos.x] == Cell.Empty);
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

        initialPlayer1Position = (playerRoom.Top, playerRoom.Left);
        initialPlayer2Position = (playerRoom.Bottom - 1, playerRoom.Right - 1);

        exitPosition = (exitRoom.Bottom - 1, exitRoom.Right);
        data[exitPosition.y, exitPosition.x] = Cell.Exit;
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
            if (data[y, middle] == Cell.Empty)
            {
                data[y, middle] = door;
                doorPositions = doorPositions.Add(y);
            }
        }

        return doorPositions;
    }
}