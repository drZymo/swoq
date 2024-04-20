namespace Swoq.Infra;

using Position = (int y, int x);

public class Map
{
    public static readonly Map Empty = new(new Cell[0, 0], 0, 0, (0, 0));

    private readonly Cell[,] data;

    public int Height { get; }
    public int Width { get; }

    public Position InitialPlayerPosition { get; }

    public Map(Cell[,] data, int height, int width, Position initialPlayerPosition)
    {
        this.data = data;
        Height = height;
        Width = width;
        InitialPlayerPosition = initialPlayerPosition;
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
