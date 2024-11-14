using Swoq.Interface;
using System.Diagnostics;

namespace Swoq.Infra;

using Position = (int y, int x);

public class OverviewBuilder(int height, int width, int visibilityRange)
{
    private Tile[] tileData = Enumerable.Repeat(Tile.Unknown, height * width).ToArray();
    private bool[] visibilityData = Enumerable.Repeat(false, height * width).ToArray();

    public void Reset()
    {
        tileData = Enumerable.Repeat(Tile.Unknown, height * width).ToArray(); ;
        PrepareForNextTimeStep();
    }

    public void PrepareForNextTimeStep()
    {
        visibilityData = Enumerable.Repeat(false, height * width).ToArray(); ;
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
                    tileData[my * width + mx] = tile;
                    visibilityData[my * width + mx] = true;
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
        return new TileMap(height, width, [.. tileData], [.. visibilityData]);
    }
}
