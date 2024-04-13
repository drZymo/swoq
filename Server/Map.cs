namespace Swoq.Server;

using Position = (int y, int x);

internal class Map
{
    private readonly Cell[,] data;

    public Map(int height, int width)
    {
        data = new Cell[height, width];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                data[y, x] = Cell.Empty;
            }
        }
        for (var x = 0; x < width; x++)
        {
            data[0, x] = Cell.Wall;
            data[height - 1, x] = Cell.Wall;
        }
        for (var y = 1; y < height - 1; y++)
        {
            data[y, 0] = Cell.Wall;
            data[y, width - 1] = Cell.Wall;
        }
    }

    public Cell this[int y, int x]
    {
        get => data[y, x];
        set { data[y, x] = value; }
    }

    public Cell this[Position pos]
    {
        get => data[pos.y, pos.x];
        set { data[pos.y, pos.x] = value; }
    }
}
