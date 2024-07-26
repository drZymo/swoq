namespace Swoq.Server;

using Position = (int y, int x);

internal static class MapEx
{
    public static void SetIsVisible(this Cell[,] map, Position pos, bool isVisible)
    {
        var cell = map[pos.y, pos.x];
        if (cell.IsVisible != isVisible)
        {
            map[pos.y, pos.x] = cell with { IsVisible = isVisible };
        }
    }

    public static void ChangeCellType(this Cell[,] map, Position pos, CellType type)
    {
        map[pos.y, pos.x] = map[pos.y, pos.x] with { Type = type };
    }

    public static void ClearCell(this Cell[,] map, Position pos)
    {
        map[pos.y, pos.x] = map[pos.y, pos.x] with { Type = CellType.Empty };
    }
}
