namespace Swoq.Server;

using Position = (int y, int x);

internal static class MapEx
{
    public static void MakeVisible(this Cell[,] map, Position pos)
    {
        map[pos.y, pos.x] = map[pos.y, pos.x] with { IsVisible = true };
    }

    public static void ChangeCellType(this Cell[,] map, Position pos, CellType type)
    {
        map[pos.y, pos.x] = map[pos.y, pos.x] with { Type = type };
    }
}
