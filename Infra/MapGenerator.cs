namespace Swoq.Infra;

using System.Collections.Immutable;

using Position = (int y, int x);

public class MapGenerator
{
    private class MapGeneratorException(string message) : Exception(message) { }

    private static readonly Random random = new();

    private readonly int height;
    private readonly int width;
    private readonly Cell[,] data;
    private Position initialPlayerPosition;
    private Position exitPosition;


    private IImmutableList<Room> rooms = ImmutableList<Room>.Empty;
    private readonly IImmutableSet<Position> allPosition = ImmutableHashSet<Position>.Empty;

    private enum KeyColor { Red, Green, Blue }

    private IImmutableSet<KeyColor> availableKeys = [KeyColor.Red, KeyColor.Green, KeyColor.Blue];

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
        return new Map(data, height, width, initialPlayerPosition);
    }

    private void GenerateLevel0()
    {
        // TODO: subclasses per level?
        CreateRoom(32, 32, 10, 10);
        initialPlayerPosition = (32 - 5, 32 - 5);
        data[32 + 4, 32 + 5] = Cell.Exit;
    }

    private void GenerateLevel1()
    {
        CreateRandomRooms(2, 10, 8);
        ConnectRoomsRandomly();

        PlacePlayerTopLeftAndExitBottomRight();
    }

    private void GenerateLevel2()
    {
        CreateRandomRooms(30, 8, 3);
        ConnectRoomsRandomly();

        PlacePlayerTopLeftAndExitBottomRight();
    }

    private void GenerateLevel3()
    {
        CreateRandomRooms(30, 15, 1);
        ConnectRoomsRandomly();

        PlacePlayerTopLeftAndExitBottomRight();
        var (keyColor, doorPos) = AddLockAroundExit();

        var infrontDoorPos = GetEmptyPositionInFront(doorPos) ?? throw new MapGeneratorException("Exit door not placed correctly");

        var keyPosition = GetFarthestPositionFromTwo(initialPlayerPosition, infrontDoorPos);
        var keyRoom = FindRoomContainingPosition(keyPosition) ?? throw new MapGeneratorException("Exit key not in a room");

        keyPosition = keyRoom.RandomPosition(1);

        data[keyPosition.y, keyPosition.x] = ToKey(keyColor);
    }



    private Room CreateRoom(int y, int x, int height, int width)
    {
        var room = new Room(y, x, height, width);

        // Make empty
        for (var my = room.Top; my < room.Bottom; my++)
            for (var mx = room.Left; mx < room.Right; mx++)
                data[my, mx] = Cell.Empty;

        rooms = rooms.Add(room);
        return room;
    }

    private void CreateRandomRooms(int maxRooms, int maxRoomSize, int margin)
    {
        var maxSize = maxRoomSize;
        while (maxSize > 4 && rooms.Count < maxRooms)
        {
            var rw = random.Next(3, maxSize);
            var rh = random.Next(3, maxSize);

            var choices = allPosition;

            // clear out edges
            for (var y = 0; y < 1 + rh / 2; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    choices = choices.Remove((y, x));
                }
            }
            for (var y = height - (rh - rh / 2); y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    choices = choices.Remove((y, x));
                }
            }
            for (var x = 0; x < 1 + rw / 2; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    choices = choices.Remove((y, x));
                }
            }
            for (var x = width - (rw - rw / 2); x < width; x++)
            {
                for (var y = 0; y < height; y++)
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

            if (choices.Count == 0)
            {
                maxSize--;
                continue;
            }

            var (ry, rx) = choices.OrderBy(_ => random.Next()).First();
            CreateRoom(ry, rx, rh, rw);
        }
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
        var roomsOrdered = rooms.OrderBy(r => r.Center.DistanceTo((0, 0)));
        var playerRoom = roomsOrdered.First();
        var exitRoom = roomsOrdered.Last();

        initialPlayerPosition = playerRoom.Center;
        exitPosition = (exitRoom.Bottom - 1, exitRoom.Right);
        data[exitPosition.y, exitPosition.x] = Cell.Exit;
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

        var posibleDoorPositions = new List<Position>();
        if (exitPosition.y < height - 2 && data[exitPosition.y + 2, exitPosition.x] == Cell.Empty)
        {
            posibleDoorPositions.Add((exitPosition.y + 1, exitPosition.x));
        }
        if (exitPosition.y > 2 && data[exitPosition.y - 2, exitPosition.x] == Cell.Empty)
        {
            posibleDoorPositions.Add((exitPosition.y - 1, exitPosition.x));
        }
        if (exitPosition.x < width - 2 && data[exitPosition.y, exitPosition.x + 2] == Cell.Empty)
        {
            posibleDoorPositions.Add((exitPosition.y, exitPosition.x + 1));
        }
        if (exitPosition.x > 2 && data[exitPosition.y, exitPosition.x - 2] == Cell.Empty)
        {
            posibleDoorPositions.Add((exitPosition.y, exitPosition.x - 1));
        }

        var doorPos = posibleDoorPositions.PickOne();

        // pick a random key
        var key = availableKeys.PickOne();
        availableKeys = availableKeys.Remove(key);

        data[doorPos.y, doorPos.x] = ToDoor(key);

        return (key, doorPos);
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
}