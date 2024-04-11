using System.Diagnostics;

namespace Swoq.Server;

using Position = (int y, int x);

internal class Game
{
    private const int VisibilityRange = 30;

    private const int Width = 20;
    private const int Height = 20;

    private Cell[,] map;

    private Position playerPos;

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

        // Exit bottom right
        map.ChangeCellType((Height - 2, Width - 2), CellType.Exit);

        // Start top left
        playerPos = (1, 1);

        UpdateVisibility();

        Debug.Assert(map[playerPos.y, playerPos.x].Type == CellType.Empty);
    }

    public Guid Id { get; } = Guid.NewGuid();

    public int[] GetState()
    {
        var state = new int[Height * Width];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                state[y * Width + x] = GetCellState((y, x));
            }
        }
        return state;
    }

    private int GetCellState(Position pos)
    {
        if (pos.Equals(playerPos))
        {
            return 1; // STATE_PLAYER
        }

        var cell = map[pos.y, pos.x];
        if (cell.IsVisible)
        {
            switch (cell.Type)
            {
                case CellType.Empty: return 2; // STATE_EMPTY
                case CellType.Wall: return 3; // STATE_WALL
                case CellType.Exit: return 4; // STATE_EXIT
            }
        }

        return 0; // STATE_UNKOWN
    }

    private void UpdateVisibility()
    {
        var minY = Math.Clamp(playerPos.y - VisibilityRange, 0, Height - 1);
        var maxY = Math.Clamp(playerPos.y + VisibilityRange, 0, Height - 1);
        var minX = Math.Clamp(playerPos.x - VisibilityRange, 0, Width - 1);
        var maxX = Math.Clamp(playerPos.x + VisibilityRange, 0, Width - 1);

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var pos = (y, x);
                if (IsVisible(pos))
                {
                    map.MakeVisible(pos);
                }
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
