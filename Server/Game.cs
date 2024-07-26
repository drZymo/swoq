using System.Diagnostics;

namespace Swoq.Server;

using Position = (int y, int x);

internal class Game
{
    private const int VisibilityRange = 5;

    public int Width { get; } = 20;
    public int Height { get; } = 20;

    private Cell[,] map;

    private Position playerPos;

    private bool isFinished = false;

    private Inventory inventory = Inventory.None;

    public Game()
    {
        // Created walled square
        map = new Cell[Height, Width];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                map[y, x] = new Cell(CellType.Empty, false);
            }
        }
        for (var x = 0; x < Width; x++)
        {
            map.ChangeCellType((0, x), CellType.Wall);
            map.ChangeCellType((Height - 1, x), CellType.Wall);
        }
        for (var y = 1; y < Height - 1; y++)
        {
            map.ChangeCellType((y, 0), CellType.Wall);
            map.ChangeCellType((y, Width - 1), CellType.Wall);
        }

        // Some obstacles
        map.ChangeCellType((3, 3), CellType.Wall);
        map.ChangeCellType((4, 3), CellType.Wall);
        map.ChangeCellType((5, 3), CellType.Wall);
        map.ChangeCellType((3, 4), CellType.Wall);

        // Exit bottom right (in the wall)
        map.ChangeCellType((Height - 2, Width - 1), CellType.Exit);

        // box around exit with red door
        map.ChangeCellType((Height - 4, Width - 2), CellType.Wall);
        map.ChangeCellType((Height - 4, Width - 3), CellType.Wall);
        map.ChangeCellType((Height - 4, Width - 4), CellType.Wall);
        map.ChangeCellType((Height - 4, Width - 5), CellType.Wall);
        map.ChangeCellType((Height - 3, Width - 5), CellType.DoorRed);
        map.ChangeCellType((Height - 2, Width - 5), CellType.Wall);

        // key
        map.ChangeCellType((4, 4), CellType.KeyRed);


        // Start top left
        playerPos = (1, 1);

        UpdateVisibility();

        Debug.Assert(map[playerPos.y, playerPos.x].Type == CellType.Empty);
    }

    public Guid Id { get; } = Guid.NewGuid();

    public GameState GetState()
    {
        var state = new int[Height * Width];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                state[y * Width + x] = GetCellState((y, x));
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

        switch (map[nextPos.y, nextPos.x].Type)
        {
            case CellType.Empty:
                // Nothing to do
                processed = true;
                break;

            case CellType.Exit:
                // TODO: game finished
                map.ClearCell(nextPos);
                processed = true;
                isFinished = true;
                break;

            case CellType.KeyRed:
                if (inventory == Inventory.None)
                {
                    // Put key in inventory and remove from map
                    inventory = Inventory.KeyRed;
                    map.ClearCell(nextPos);
                    processed = true;
                }
                break;

            default:
                throw new NotImplementedException(); // Should not be possible
        }

        if (processed)
        {
            playerPos = nextPos;

            UpdateVisibility();
        }
        Debug.Assert(map[playerPos.y, playerPos.x].Type == CellType.Empty);

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

        switch (map[usePos.y, usePos.x].Type)
        {
            case CellType.Empty:
            case CellType.Exit:
            case CellType.Wall:
            case CellType.KeyRed:
                // Cannot use on this
                break;

            case CellType.DoorRed:
                if (inventory == Inventory.KeyRed)
                {
                    // Remove key from inventory and open door (by making the cell empty)
                    inventory = Inventory.None;
                    map.ClearCell(usePos);
                    processed = true;
                }
                break;

            default:
                throw new NotImplementedException(); // Should not be possible
        }

        if (processed)
        {
            UpdateVisibility();
        }
        Debug.Assert(map[playerPos.y, playerPos.x].Type == CellType.Empty);

        return processed;
    }

    private bool CanMoveTo(Position position)
    {
        if (position.x < 0 || position.x >= Width) return false;
        if (position.y < 0 || position.y >= Height) return false;

        var cell = map[position.y, position.x];

        return CanWalkOn(cell.Type);
    }

    private static bool CanWalkOn(CellType cellType) => cellType switch
    {
        CellType.Empty => true,
        CellType.Exit => true,
        CellType.KeyRed => true,
        _ => false,
    };

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

        var cell = map[pos.y, pos.x];
        if (cell.IsVisible)
        {
            switch (cell.Type)
            {
                case CellType.Empty: return EMPTY;
                case CellType.Wall: return WALL;
                case CellType.Exit: return EXIT;
                case CellType.DoorRed: return DOOR_RED;
                case CellType.KeyRed: return KEY_RED;
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

    private void UpdateVisibility()
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var pos = (y, x);
                map.SetIsVisible(pos, IsVisible(pos));
            }
        }
    }

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
        var length = Math.Sqrt(dx * dx + dy * dy);

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
                if (mapX < 0 || mapX >= Width || mapY < 0 || mapY >= Height) break;
                if (map[mapY, mapX].Type == CellType.Wall) return false; // Blocked by wall

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
                if (mapX < 0 || mapX >= Width || mapY < 0 || mapY >= Height) break;
                if (map[mapY, mapX].Type == CellType.Wall) return false; // Blocked by wall

                y += stepY;
                x += stepX;
            }
        }

        return true;
    }
}
