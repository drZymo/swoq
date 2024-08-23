using Swoq.Interface;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Swoq.Infra;

using Position = (int y, int x);

public class MapBuilder(int height, int width, int visibilityRange)
{
    private readonly Cell[,] mapData = new Cell[height, width];
    private readonly bool[,] visibility = new bool[height, width];

    private int level = 0;
    private Position player1Position = (-1, -1);
    private Position? player2Position = null;
    private IImmutableList<Position> enemyPositions = ImmutableList<Position>.Empty;

    public void Reset()
    {
        level = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                mapData[y, x] = Cell.Unknown;
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
                visibility[y, x] = false;
            }
        }
        player1Position = (-1, -1);
        player2Position = null;
        enemyPositions = ImmutableList<Position>.Empty;
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
                if (tile == Tile.Enemy)
                {
                    enemyPositions = enemyPositions.Add((my, mx));
                }
                var cell = ToCell(tile);
                if (cell != Cell.Unknown)
                {
                    mapData[my, mx] = cell;
                    visibility[my, mx] = true;
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

    public Map CreateMap()
    {
        Position? enemy1Pos = enemyPositions.Count > 0 ? enemyPositions[0] : null;
        Position? enemy2Pos = enemyPositions.Count > 1 ? enemyPositions[1] : null;

        return new Map(level, mapData.Cast<Cell>(), height, width, player1Position,
            initialPlayer2Position: player2Position,
            initialEnemy1Position: enemy1Pos,
            initialEnemy2Position: enemy2Pos,
            visibility: visibility.Cast<bool>());
    }

    private static Cell ToCell(Tile tile) => tile switch
    {
        Tile.Unknown => Cell.Unknown,
        Tile.Empty => Cell.Empty,
        Tile.Wall => Cell.Wall,
        Tile.Exit => Cell.Exit,
        Tile.DoorRed => Cell.DoorRedClosed,
        Tile.KeyRed => Cell.KeyRed,
        Tile.DoorGreen => Cell.DoorGreenClosed,
        Tile.KeyGreen => Cell.KeyGreen,
        Tile.DoorBlue => Cell.DoorBlueClosed,
        Tile.KeyBlue => Cell.KeyBlue,
        Tile.PressurePlateRed => Cell.PressurePlateRed,
        Tile.PressurePlateGreen => Cell.PressurePlateGreen,
        Tile.PressurePlateBlue => Cell.PressurePlateBlue,
        Tile.Sword => Cell.Sword,
        Tile.Health => Cell.Health,
        Tile.Boulder => Cell.Boulder,
        Tile.Treasure => Cell.Treasure,

        // Players and enemies always stand on empty cells
        Tile.Player => Cell.Empty,
        Tile.Enemy => Cell.Empty,
        Tile.Boss => Cell.Empty,
        _ => throw new NotImplementedException(),
    };
}
