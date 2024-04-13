using System.Diagnostics;

namespace Swoq.Server;

using Position = (int y, int x);

internal class Game
{
    private const int VisibilityRange = 5;

    public const int Width = 20;
    public const int Height = 20;

    private readonly Map map;
    private readonly int level;
    private Position playerPos;

    private bool isFinished = false;

    private Inventory inventory = Inventory.None;

    public Game(int level)
    {
        // Created walled square
        map = new Map(Height, Width);
        this.level = level;

        // Some obstacles
        map[3, 3] = Cell.Wall;
        map[4, 3] = Cell.Wall;
        map[5, 3] = Cell.Wall;
        map[3, 4] = Cell.Wall;

        // Exit bottom right (in the wall)
        map[map.Height - 2, map.Width - 1] = Cell.Exit;

        // box around exit with red door
        map[map.Height - 4, map.Width - 2] = Cell.Wall;
        map[map.Height - 4, map.Width - 3] = Cell.Wall;
        map[map.Height - 4, map.Width - 4] = Cell.Wall;
        map[map.Height - 4, map.Width - 5] = Cell.Wall;
        map[map.Height - 3, map.Width - 5] = Cell.DoorRed;
        map[map.Height - 2, map.Width - 5] = Cell.Wall;

        // key
        map[4, 4] = Cell.KeyRed;

        // Start top left
        playerPos = (1, 1);

        Debug.Assert(map[playerPos] == Cell.Empty);
    }

    public Guid Id { get; } = Guid.NewGuid();

    public GameState GetState()
    {
        var state = new int[map.Height * map.Width];
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                state[y * map.Width + x] = GetCellState((y, x));
            }
        }
        return new GameState(state, isFinished, GetInventoryState());
    }

    public bool Move(Direction direction)
    {
        if (isFinished) return false;

        Position nextPos = direction switch
        {
            Direction.North => (playerPos.y - 1, playerPos.x),
            Direction.East => (playerPos.y, playerPos.x + 1),
            Direction.South => (playerPos.y + 1, playerPos.x),
            Direction.West => (playerPos.y, playerPos.x - 1),
            _ => throw new NotImplementedException(), // Should not be possible
        };

        if (!CanMoveTo(nextPos)) return false;

        var processed = false;

        switch (map[nextPos])
        {
            case Cell.Empty:
                // Nothing to do
                processed = true;
                break;

            case Cell.Exit:
                // TODO: game finished
                map[nextPos] = Cell.Empty;
                processed = true;
                isFinished = true;
                break;

            case Cell.KeyRed:
                if (inventory == Inventory.None)
                {
                    // Put key in inventory and remove from map
                    inventory = Inventory.KeyRed;
                    map[nextPos] = Cell.Empty;
                    processed = true;
                }
                break;

            default:
                throw new NotImplementedException(); // Should not be possible
        }

        if (processed)
        {
            playerPos = nextPos;
        }
        Debug.Assert(map[playerPos] == Cell.Empty);

        return processed;
    }

    public bool Use(Direction direction)
    {
        if (isFinished) return false;
        if (inventory == Inventory.None) return false;

        Position usePos = direction switch
        {
            Direction.North => (playerPos.y - 1, playerPos.x),
            Direction.East => (playerPos.y, playerPos.x + 1),
            Direction.South => (playerPos.y + 1, playerPos.x),
            Direction.West => (playerPos.y, playerPos.x - 1),
            _ => throw new NotImplementedException(), // Should not be possible
        };

        var processed = false;

        switch (map[usePos])
        {
            case Cell.Empty:
            case Cell.Exit:
            case Cell.Wall:
            case Cell.KeyRed:
                // Cannot use on this
                break;

            case Cell.DoorRed:
                if (inventory == Inventory.KeyRed)
                {
                    // Remove key from inventory and open door (by making the cell empty)
                    inventory = Inventory.None;
                    map[usePos] = Cell.Empty;
                    processed = true;
                }
                break;

            default:
                throw new NotImplementedException(); // Should not be possible
        }

        return processed;
    }

    private bool CanMoveTo(Position position)
    {
        if (position.x < 0 || position.x >= map.Width) return false;
        if (position.y < 0 || position.y >= map.Height) return false;

        var cell = map[position];

        return CanWalkOn(cell);
    }

    private static bool CanWalkOn(Cell cell) => cell switch
    {
        Cell.Empty => true,
        Cell.Exit => true,
        Cell.KeyRed => true,
        _ => false,
    };

    private bool IsVisible(Position pos)
    {
        if (pos.DistanceTo(playerPos) >= VisibilityRange) return false;

        if (playerPos.x < pos.x && IsVisible(playerPos.x + 0.5, playerPos.y + 0.5, pos.x, pos.y + 0.5)) return true;
        if (playerPos.x > pos.x && IsVisible(playerPos.x + 0.5, playerPos.y + 0.5, pos.x + 1, pos.y + 0.5)) return true;
        if (playerPos.y < pos.y && IsVisible(playerPos.x + 0.5, playerPos.y + 0.5, pos.x + 0.5, pos.y)) return true;
        if (playerPos.y > pos.y && IsVisible(playerPos.x + 0.5, playerPos.y + 0.5, pos.x + 0.5, pos.y + 1)) return true;

        return false;
    }

    private bool IsVisible(double srcX, double srcY, double dstX, double dstY)
    {
        var dx = dstX - srcX;
        var dy = dstY - srcY;

        if (Math.Abs(dx) > 1e-6) // prevent division by small amount
        {
            var stepX = Math.Sign(dx);
            var x = stepX > 0 ? Math.Ceiling(srcX) : Math.Floor(srcX);
            var stepY = stepX * dy / dx;
            var y = srcY + (x - srcX) * dy / dx;

            while (!(srcX < dstX && x >= dstX) && !(srcX > dstX && x <= dstX))
            {
                var mapX = (int)(x + stepX * 0.5);
                var mapY = (int)y;
                if (mapX < 0 || mapX >= map.Width || mapY < 0 || mapY >= map.Height) break;
                if (map[mapY, mapX] == Cell.Wall) return false; // Blocked by wall

                x += stepX;
                y += stepY;
            }
        }

        if (Math.Abs(dy) > 1e-6) // prevent division by small amount
        {
            var stepY = Math.Sign(dy);
            var y = stepY > 0 ? Math.Ceiling(srcY) : Math.Floor(srcY);
            var stepX = stepY * dx / dy;
            var x = srcX + (y - srcY) * dx / dy;

            while (!(srcY < dstY && y >= dstY) && !(srcY > dstY && y <= dstY))
            {
                var mapY = (int)(y + stepY * 0.5);
                var mapX = (int)x;
                if (mapX < 0 || mapX >= map.Width || mapY < 0 || mapY >= map.Height) break;
                if (map[mapY, mapX] == Cell.Wall) return false; // Blocked by wall

                y += stepY;
                x += stepX;
            }
        }

        return true;
    }


    #region State

    private int GetCellState(Position pos)
    {
        const int UNKNOWN = 0;
        const int EMPTY = 1;
        const int PLAYER = 2;
        const int WALL = 3;
        const int EXIT = 4;

        const int DOOR_RED = 5;
        const int KEY_RED = 6;

        if (pos.Equals(playerPos))
        {
            return PLAYER;
        }

        if (IsVisible(pos))
        {
            var cell = map[pos];
            switch (cell)
            {
                case Cell.Empty: return EMPTY;
                case Cell.Wall: return WALL;
                case Cell.Exit: return EXIT;
                case Cell.DoorRed: return DOOR_RED;
                case Cell.KeyRed: return KEY_RED;
            }
        }

        return UNKNOWN;
    }

    private int GetInventoryState() => inventory switch
    {
        Inventory.None => 0,
        Inventory.KeyRed => 1,
        _ => throw new NotImplementedException(),
    };

    #endregion
}
