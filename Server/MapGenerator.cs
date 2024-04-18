namespace Swoq.Server;

using System.Collections.Immutable;
using Position = (int y, int x);

internal class MapGenerator
{
    private static readonly Random random = new();

    private readonly int height;
    private readonly int width;
    private readonly Cell[,] data;
    private Position initialPlayerPosition;

    private record Room(int Y, int X, int Height, int Width)
    {
        public Position Center => (Y, X);
        public int Top => Y - Height / 2;
        public int Bottom => Top + Height;
        public int Left => X - Width / 2;
        public int Right => Left + Width;
    }

    private IImmutableList<Room> rooms = ImmutableList<Room>.Empty;
    private IImmutableSet<Position> allPosition = ImmutableHashSet<Position>.Empty;

    public static Map Generate(int level, int height = 64, int width = 64)
    {
        var generator = new MapGenerator(height, width);
        return generator.Generate(level);
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
        CreateRandomRooms(30, 15, 1);
        ConnectRoomsRandomly();

        PlacePlayerTopLeftAndExitBottomRight();
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
            for (var y = height - 1 - (rh - rh / 2); y < height; y++)
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
            for (var x = width - 1 - (rw - rw / 2); x < width; x++)
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
        var todo = rooms;

        // start with top left room
        var topLeft = todo.OrderBy(r => r.Center.DistanceTo((0, 0))).First();
        var current = topLeft;

        while (todo.Count > 1)
        {
            todo = todo.Remove(current);

            var closestRooms = todo.OrderBy(r => r.Center.DistanceTo(current.Center)).Take(2);
            // pick one random
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
        data[exitRoom.Bottom - 1, exitRoom.Right] = Cell.Exit;
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

    private void ConnectRooms(Room room1, Room room2)
    {
        var dx = room2.X - room1.X;
        var dy = room2.Y - room1.Y;

        if (Math.Abs(dx) > Math.Abs(dy))
        {
            if (room2.X != room1.X)
            {
                var dir = Math.Sign(dx);
                for (var x = room1.X; x < room2.X + dir; x += dir)
                {
                    data[room1.Y, x] = Cell.Empty;
                    data[room1.Y - dir, x] = Cell.Empty;
                }
            }
            if (room2.Y != room1.Y)
            {
                var dir = Math.Sign(dy);
                for (var y = room1.Y; y < room2.Y + dir; y += dir)
                {
                    data[y, room2.X] = Cell.Empty;
                    data[y, room2.X - dir] = Cell.Empty;
                }
            }
        }
        else
        {
            if (room2.Y != room1.Y)
            {
                var dir = Math.Sign(dy);
                for (var y = room1.Y; y < room2.Y + dir; y += dir)
                {
                    data[y, room1.X] = Cell.Empty;
                    data[y, room1.X - dir] = Cell.Empty;
                }
            }
            if (room2.X != room1.X)
            {
                var dir = Math.Sign(dx);
                for (var x = room1.X; x < room2.X + dir; x += dir)
                {
                    data[room2.Y, x] = Cell.Empty;
                    data[room2.Y - dir, x] = Cell.Empty;
                }
            }
        }
    }
}
