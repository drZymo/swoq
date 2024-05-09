namespace Swoq.Infra;

using System.Collections;
using System.Collections.Immutable;
using Position = (int y, int x);

public class Map(
    IEnumerable<Cell> data,
    int height,
    int width,
    Position initialPlayer1Position,
    Position? initialPlayer2Position = null,
    Position? initialEnemy1Position = null,
    Inventory initialEnemy1Inventory = Inventory.None,
    Position? initialEnemy2Position = null,
    Inventory initialEnemy2Inventory = Inventory.None,
    IEnumerable<bool>? visibility = null) : IEnumerable<Cell>
{
    public static readonly Map Empty = new([], 0, 0, (0, 0));

    private readonly IImmutableList<Cell> cells = data.ToImmutableArray();
    private readonly IImmutableList<bool>? visibility = visibility?.ToImmutableArray();

    public int Height { get; } = height;
    public int Width { get; } = width;

    public Position InitialPlayer1Position { get; } = initialPlayer1Position;
    public Position? InitialPlayer2Position { get; } = initialPlayer2Position;
    public Position? InitialEnemy1Position { get; } = initialEnemy1Position;
    public Inventory InitialEnemy1Inventory { get; } = initialEnemy1Inventory;
    public Position? InitialEnemy2Position { get; } = initialEnemy2Position;
    public Inventory InitialEnemy2Inventory { get; } = initialEnemy2Inventory;

    public Cell this[int y, int x] => cells[y * Width + x];

    public Cell this[Position pos] => this[pos.y, pos.x];

    public IEnumerator<Cell> GetEnumerator() => cells.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => cells.GetEnumerator();

    public Map Set(int y, int x, Cell cell)
    {
        var cells = this.cells.SetItem(y * Width + x, cell);
        return new Map(cells, Height, Width, InitialPlayer1Position, InitialPlayer2Position, InitialEnemy1Position, InitialEnemy1Inventory, InitialEnemy2Position, InitialEnemy2Inventory, visibility);
    }

    public Map Set(Position pos, Cell cell) => Set(pos.y, pos.x, cell);


    public bool IsVisible(int y, int x) => visibility == null || visibility[y * Width + x];

    public bool IsVisible(Position pos) => IsVisible(pos.y, pos.x);
}
