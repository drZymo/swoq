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
    private Position player1Position = (-1, -1);
    private Position? player2Position = null;
    private IImmutableList<Position> enemyPositions = ImmutableList<Position>.Empty;
    private Position? bossPosition = null;

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
        player1Position = (-1, -1);
        player2Position = null;
        enemyPositions = ImmutableList<Position>.Empty;
        bossPosition = null;
    }

    public void SetLevel(int level)
    {
        this.level = level;
    }

    public void AddPlayerState(Position playerPosition, IEnumerable<Tile> surroundings, int playerIndex)
    {
        if (playerIndex < 1 || playerIndex > 2) throw new ArgumentOutOfRangeException(nameof(playerIndex));

        if (playerPosition.y < 0 || playerPosition.x < 0 ||
            !surroundings.Any())
        {
            if (playerIndex == 1) player1Position = PositionEx.Invalid;
            if (playerIndex == 2) player2Position = null;
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
                if (tile == Tile.Boss)
                {
                    bossPosition = (my, mx);
                }
                else if (tile == Tile.Enemy)
                {
                    enemyPositions = enemyPositions.Add((my, mx));
                }
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

        if (playerIndex == 1) player1Position = (playerPosition.y, playerPosition.x);
        if (playerIndex == 2) player2Position = (playerPosition.y, playerPosition.x);
    }

    public Overview CreateOverview()
    {
        // Cast<T> will make it a flat array
        return new Overview(level, height, width, tileData.Cast<Tile>(), visibilityData.Cast<bool>());
    }
}
