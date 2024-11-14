using Swoq.Interface;
using System.Collections.Immutable;

namespace Swoq.Infra;

public record TileMap(int Height, int Width, ImmutableArray<Tile> Tiles, ImmutableArray<bool> Visibility)
{
    public static readonly TileMap Empty = new(0, 0, [], []);
}
