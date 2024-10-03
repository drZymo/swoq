namespace Swoq.Infra;

using Swoq.Interface;
using System.Collections.Immutable;
using Position = (int y, int x);

public record Player(Position Position, Inventory Inventory);
public record Enemy(Position Position, Inventory Inventory, bool IsBoss);

public class Map(
    int level,
    int height,
    int width,
    IEnumerable<Cell> data,
    Player player1,
    Player player2,
    Enemy enemy1,
    Enemy enemy2,
    Enemy enemy3,
    bool isFinal = false)
{
    public static readonly Map Empty = new(
        -1,
        0,
        0,
        [],
        new Player(PositionEx.Invalid, Inventory.None),
        new Player(PositionEx.Invalid, Inventory.None),
        new Enemy(PositionEx.Invalid, Inventory.None, false),
        new Enemy(PositionEx.Invalid, Inventory.None, false),
        new Enemy(PositionEx.Invalid, Inventory.None, false));

    private readonly IImmutableList<Cell> cells = data.ToImmutableArray();

    public int Level { get; } = level;
    public int Height { get; } = height;
    public int Width { get; } = width;

    public Player Player1 { get; } = player1;
    public Player Player2 { get; } = player2;
    public Enemy Enemy1 { get; } = enemy1;
    public Enemy Enemy2 { get; } = enemy2;
    public Enemy Enemy3 { get; } = enemy3;


    public bool IsFinal { get; } = isFinal;

    public Cell this[int y, int x] => cells[y * Width + x];

    public Cell this[Position pos] => this[pos.y, pos.x];

    public Map Set(int y, int x, Cell cell)
    {
        var cells = this.cells.SetItem(y * Width + x, cell);
        return new Map(Level, Height, Width, cells, Player1, Player2, Enemy1, Enemy2, Enemy3, IsFinal);
    }

    public Map Set(Position pos, Cell cell) => Set(pos.y, pos.x, cell);
}
