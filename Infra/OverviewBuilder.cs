using Swoq.Interface;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Swoq.Infra;

using Position = (int y, int x);

public class OverviewBuilder(int height, int width, int visibilityRange)
{
    private readonly Tile[,] tileData = new Tile[height, width];
    private readonly bool[,] visibilityData = new bool[height, width];

    private int level = 0;

    public void Reset()
    {
        level = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                tileData[y, x] = Tile.Unknown;
            }
        }
        PrepareForNextTimeStep();
    }

    public void PrepareForNextTimeStep()
    {
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                visibilityData[y, x] = false;
            }
        }
    }

    public void SetLevel(int level)
    {
        this.level = level;
    }

    public void AddPlayerState(Position playerPosition, IEnumerable<Tile> surroundings, int playerIndex)
    {
        if (playerIndex < 1 || playerIndex > 2) throw new ArgumentOutOfRangeException(nameof(playerIndex));

        if (playerPosition.y < 0 || playerPosition.x < 0 || !surroundings.Any())
        {
            return;
        }

        var surroundingsSize = visibilityRange * 2 + 1;

        var top = playerPosition.y - visibilityRange;
        var left = playerPosition.x - visibilityRange;

        var y = 0;
        var x = 0;
        foreach (var tile in surroundings)
        {
            Debug.Assert(0 <= x && x < surroundingsSize);
            Debug.Assert(0 <= y && y < surroundingsSize);

            var my = top + y;
            var mx = left + x;
            if (0 <= my && my < height && 0 <= mx && mx < width)
            {
                if (tile != Tile.Unknown)
                {
                    tileData[my, mx] = tile;
                    visibilityData[my, mx] = true;
                }
            }

            x++;
            if (x >= surroundingsSize)
            {
                y++;
                x = 0;
            }
        }
    }

    public TileMap CreateOverview()
    {
        // Cast<T> will make it a flat array
        return new TileMap(height, width, tileData.Cast<Tile>().ToImmutableArray(), visibilityData.Cast<bool>().ToImmutableArray());
    }
}
