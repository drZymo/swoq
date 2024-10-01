namespace Swoq.Infra;

using Swoq.Interface;
using System.Collections;
using System.Collections.Immutable;
using Position = (int y, int x);

public class Overview(
    int level,
    int height,
    int width,
    IEnumerable<Tile> tiles,
    IEnumerable<bool> visibility) : IEnumerable<Tile>
{
    public static readonly Overview Empty = new(-1, 0, 0, [], []);

    private readonly IImmutableList<Tile> tiles = tiles.ToImmutableArray();
    private readonly IImmutableList<bool> visibility = visibility.ToImmutableArray();

    public int Level { get; } = level;
    public int Height { get; } = height;
    public int Width { get; } = width;

    public Tile this[int y, int x] => tiles[y * Width + x];

    public Tile this[Position pos] => this[pos.y, pos.x];

    public IEnumerator<Tile> GetEnumerator() => tiles.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => tiles.GetEnumerator();

    public Overview Set(int y, int x, Tile tile)
    {
        var tiles = this.tiles.SetItem(y * Width + x, tile);
        return new Overview(
            Level,
            Height,
            Width,
            tiles,
            visibility);
    }

    public Overview Set(Position pos, Tile tile) => Set(pos.y, pos.x, tile);

    public bool IsVisible(int y, int x) => visibility == null || visibility[y * Width + x];

    public bool IsVisible(Position pos) => IsVisible(pos.y, pos.x);
}
