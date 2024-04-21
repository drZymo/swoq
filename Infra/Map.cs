namespace Swoq.Infra;

using Position = (int y, int x);

public class Map
{
    public static readonly Map Empty = new(new Cell[0, 0], 0, 0, (0, 0));

    private readonly Cell[,] data;

    public int Height { get; }
    public int Width { get; }

    public Position InitialPlayer1Position { get; }
    public Position? InitialPlayer2Position { get; } = null;
    public Position? InitialEnemy1Position { get; } = null;
    public Inventory InitialEnemy1Inventory { get; } = Inventory.None;
    public Position? InitialEnemy2Position { get; } = null;
    public Inventory InitialEnemy2Inventory { get; } = Inventory.None;
    public Map(
        Cell[,] data,
        int height,
        int width,
        Position initialPlayer1Position,
        Position? initialPlayer2Position = null,
        Position? initialEnemy1Position = null,
        Inventory initialEnemy1Inventory = Inventory.None,
        Position? initialEnemy2Position = null,
        Inventory initialEnemy2Inventory = Inventory.None)
    {
        this.data = data;
        Height = height;
        Width = width;
        InitialPlayer1Position = initialPlayer1Position;
        InitialPlayer2Position = initialPlayer2Position;
        InitialEnemy1Position = initialEnemy1Position;
        InitialEnemy1Inventory = initialEnemy1Inventory;
        InitialEnemy2Position = initialEnemy2Position;
        InitialEnemy2Inventory = initialEnemy2Inventory;
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
