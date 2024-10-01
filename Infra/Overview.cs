namespace Swoq.Infra;

using Swoq.Interface;
using System.Collections;
using System.Collections.Immutable;
using Position = (int y, int x);

public class Overview(
    int level,
    IEnumerable<Tile> data,
    int height,
    int width,
    Position initialPlayer1Position,
    Position? initialPlayer2Position = null,
    Position? initialEnemy1Position = null,
    Inventory initialEnemy1Inventory = Inventory.None,
    bool isEnemy1Boss = false,
    Position? initialEnemy2Position = null,
    Inventory initialEnemy2Inventory = Inventory.None,
    Position? initialEnemy3Position = null,
    Inventory initialEnemy3Inventory = Inventory.None,
    IEnumerable<bool>? visibility = null,
    bool isFinal = false) : IEnumerable<Tile>
{
    public static readonly Overview Empty = new(-1, [], 0, 0, (0, 0));

    private readonly IImmutableList<Tile> tiles = data.ToImmutableArray();
    private readonly IImmutableList<bool>? visibility = visibility?.ToImmutableArray();

    public int Level { get; } = level;
    public int Height { get; } = height;
    public int Width { get; } = width;

    public Position InitialPlayer1Position { get; } = initialPlayer1Position;
    public Position? InitialPlayer2Position { get; } = initialPlayer2Position;
    public Position? InitialEnemy1Position { get; } = initialEnemy1Position;
    public Inventory InitialEnemy1Inventory { get; } = initialEnemy1Inventory;
    public bool IsEnemy1Boss { get; } = isEnemy1Boss;
    public Position? InitialEnemy2Position { get; } = initialEnemy2Position;
    public Inventory InitialEnemy2Inventory { get; } = initialEnemy2Inventory;
    public Position? InitialEnemy3Position { get; } = initialEnemy3Position;
    public Inventory InitialEnemy3Inventory { get; } = initialEnemy3Inventory;

    public bool IsFinal { get; } = isFinal;

    public Tile this[int y, int x] => tiles[y * Width + x];

    public Tile this[Position pos] => this[pos.y, pos.x];

    public IEnumerator<Tile> GetEnumerator() => tiles.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => tiles.GetEnumerator();

    public Overview Set(int y, int x, Tile tile)
    {
        var tiles = this.tiles.SetItem(y * Width + x, tile);
        return new Overview(
            Level,
            tiles,
            Height,
            Width,
            InitialPlayer1Position,
            InitialPlayer2Position,
            InitialEnemy1Position,
            InitialEnemy1Inventory,
            IsEnemy1Boss,
            InitialEnemy2Position,
            InitialEnemy2Inventory,
            InitialEnemy3Position,
            InitialEnemy3Inventory,
            visibility,
            IsFinal);
    }

    public Overview Set(Position pos, Tile tile) => Set(pos.y, pos.x, tile);

    public bool IsVisible(int y, int x) => visibility == null || visibility[y * Width + x];

    public bool IsVisible(Position pos) => IsVisible(pos.y, pos.x);
}
