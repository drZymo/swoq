using System.Diagnostics;

namespace Swoq.Server;

using Position = (int y, int x);

internal class Game
{
    public const int VisibilityRange = 5;

    private readonly Map map;
    private readonly int level;
    private Position playerPos;

    private bool isFinished = false;

    private Inventory inventory = Inventory.None;

    public Game(int level)
    {
        //map = new Map(32, 32);
        map = Map.LoadFromFile($"../level{level}.bin");
        playerPos = map.InitialPlayerPosition;
        this.level = level;

        Debug.Assert(map[playerPos] == Cell.Empty);
    }

    public Guid Id { get; } = Guid.NewGuid();
    public int Width => map.Width;
    public int Height => map.Height;

    public GameState GetState()
    {
        var width = VisibilityRange * 2 + 1;
        var height = VisibilityRange * 2 + 1;
        var state = new int[height * width];

        var top = playerPos.y - VisibilityRange;
        var left = playerPos.x - VisibilityRange;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                state[y * width + x] = GetCellState((top + y, left + x));
            }
        }
        return new GameState(playerPos.x, playerPos.y, state, isFinished, GetInventoryState());
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
            case Cell.DoorRedOpen:
            case Cell.DoorGreenOpen:
            case Cell.DoorBlueOpen:
                // Nothing to do
                processed = true;
                break;

            case Cell.Exit:
                // Game finished
                map[nextPos] = Cell.Empty;
                isFinished = true;
                processed = true;
                break;

            case Cell.KeyRed:
            case Cell.KeyGreen:
            case Cell.KeyBlue:
                processed = TryPickup(nextPos);
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
            case Cell.DoorRedOpen:
            case Cell.KeyRed:
            case Cell.DoorGreenOpen:
            case Cell.KeyGreen:
            case Cell.DoorBlueOpen:
            case Cell.KeyBlue:
                // Cannot use on this
                break;

            case Cell.DoorRedClosed:
                processed = TryUseItemOnClosedDoor(usePos, Inventory.KeyRed);
                break;
            case Cell.DoorGreenClosed:
                processed = TryUseItemOnClosedDoor(usePos, Inventory.KeyGreen);
                break;
            case Cell.DoorBlueClosed:
                processed = TryUseItemOnClosedDoor(usePos, Inventory.KeyBlue);
                break;

            default:
                throw new NotImplementedException(); // Should not be possible
        }

        return processed;
    }

    private bool TryPickup(Position position)
    {
        // Cannot pickup if inventory is full
        if (inventory != Inventory.None) return false;

        // Is it an item that can be picked up?
        var item = ToInventory(map[position]);
        if (item == Inventory.None) return false;

        // Put in inventory and remove from map
        inventory = item;
        map[position] = Cell.Empty;
        return true;
    }

    private bool TryUseItemOnClosedDoor(Position position, Inventory item)
    {
        // Cannot use if inventory is empty
        if (inventory == Inventory.None) return false;

        // Cannot use if item is not in inventory
        if (inventory != item) return false;

        // Remove from inventory and change to open
        inventory = Inventory.None;
        map[position] = ToOpenDoor(map[position]);
        return true;
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
        Cell.DoorRedOpen => true,
        Cell.KeyRed => true,
        Cell.DoorGreenOpen => true,
        Cell.KeyGreen => true,
        Cell.DoorBlueOpen => true,
        Cell.KeyBlue => true,

        Cell.Wall => false,
        Cell.DoorRedClosed => false,
        Cell.DoorGreenClosed => false,
        Cell.DoorBlueClosed => false,
        _ => false,
    };

    private bool IsVisible(Position pos)
    {
        if (pos.y < 0 || pos.y >= map.Height) return false;
        if (pos.x < 0 || pos.x >= map.Width) return false;
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
                if (!CanWalkOn(map[mapY, mapX])) return false; // Blocked by wall

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
                if (!CanWalkOn(map[mapY, mapX])) return false; // Blocked by wall

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
        const int DOOR_GREEN = 7;
        const int KEY_GREEN = 8;
        const int DOOR_BLUE = 9;
        const int KEY_BLUE = 10;

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
                case Cell.DoorRedClosed: return DOOR_RED;
                case Cell.KeyRed: return KEY_RED;
                case Cell.DoorGreenClosed: return DOOR_GREEN;
                case Cell.KeyGreen: return KEY_GREEN;
                case Cell.DoorBlueClosed: return DOOR_BLUE;
                case Cell.KeyBlue: return KEY_BLUE;

                // don't show open doors
                case Cell.DoorRedOpen:
                case Cell.DoorGreenOpen:
                case Cell.DoorBlueOpen:
                    return EMPTY;
            }
        }

        return UNKNOWN;
    }

    private int GetInventoryState() => inventory switch
    {
        Inventory.None => 0,
        Inventory.KeyRed => 1,
        Inventory.KeyGreen => 2,
        Inventory.KeyBlue => 3,
        _ => throw new NotImplementedException(),
    };

    #endregion

    private static Inventory ToInventory(Cell cell) => cell switch
    {
        Cell.KeyRed => Inventory.KeyRed,
        Cell.KeyGreen => Inventory.KeyGreen,
        Cell.KeyBlue => Inventory.KeyBlue,
        _ => Inventory.None,
    };

    private static Cell ToOpenDoor(Cell closedDoor) => closedDoor switch
    {
        Cell.DoorRedClosed => Cell.DoorRedOpen,
        Cell.DoorGreenClosed => Cell.DoorGreenOpen,
        Cell.DoorBlueClosed => Cell.DoorBlueOpen,
        _ => throw new NotImplementedException("Not a closed door"),
    };
}
