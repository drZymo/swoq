namespace Swoq.Infra;

using Position = (int y, int x);

public class Map(
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
    public static readonly Map Empty = new(new Cell[0, 0], 0, 0, (0, 0));

    private readonly Cell[,] data = data;

    public int Height { get; } = height;
    public int Width { get; } = width;

    public Position InitialPlayer1Position { get; } = initialPlayer1Position;
    public Position? InitialPlayer2Position { get; } = initialPlayer2Position;
    public Position? InitialEnemy1Position { get; } = initialEnemy1Position;
    public Inventory InitialEnemy1Inventory { get; } = initialEnemy1Inventory;
    public Position? InitialEnemy2Position { get; } = initialEnemy2Position;
    public Inventory InitialEnemy2Inventory { get; } = initialEnemy2Inventory;

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
