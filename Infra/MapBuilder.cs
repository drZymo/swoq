using System.Collections.Immutable;

namespace Swoq.Infra;

using Position = (int y, int x);

public class MapBuilder(int height, int width, int visibilityRange)
{
    private const int CellStateEnemy = 14;
    private readonly Cell[,] mapData = new Cell[height, width];
    private readonly bool[,] visibility = new bool[height, width];

    private Position player1Position = (-1, -1);
    private Position? player2Position = null;
    private IImmutableList<Position> enemyPositions = ImmutableList<Position>.Empty;

    public void Reset()
    {
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

    public void AddPlayerState(Position playerPosition, IReadOnlyList<int> surroundings, int playerIndex)
    {
        if (playerIndex < 1 || playerIndex > 2) throw new ArgumentOutOfRangeException(nameof(playerIndex));

        if (playerPosition.y < 0 || playerPosition.x < 0 ||
            surroundings.Count <= 0)
        {
            if (playerIndex == 1) player1Position = PositionEx.Invalid;
            if (playerIndex == 2) player2Position = null;
            return;
        }

        var top = playerPosition.y - visibilityRange;
        var left = playerPosition.x - visibilityRange;

        var i = 0;
        for (var y = 0; y < visibilityRange * 2 + 1; y++)
        {
            for (var x = 0; x < visibilityRange * 2 + 1; x++)
            {
                var my = top + y;
                var mx = left + x;
                if (0 <= my && my < height && 0 <= mx && mx < width)
                {
                    int cellState = surroundings[i];
                    if (cellState == CellStateEnemy)
                    {
                        enemyPositions = enemyPositions.Add((my, mx));
                    }
                    var cell = ToCell(cellState);
                    if (cell != Cell.Unknown)
                    {
                        mapData[my, mx] = cell;
                        visibility[my, mx] = true;
                    }
                }
                i++;
            }
        }

        if (playerIndex == 1) player1Position = (playerPosition.y, playerPosition.x);
        if (playerIndex == 2) player2Position = (playerPosition.y, playerPosition.x);
    }

    public Map CreateMap()
    {
        Position? enemy1Pos = enemyPositions.Count > 0 ? enemyPositions[0] : null;
        Position? enemy2Pos = enemyPositions.Count > 1 ? enemyPositions[1] : null;

        return new Map(mapData.Cast<Cell>(), height, width, player1Position,
            initialPlayer2Position: player2Position,
            initialEnemy1Position: enemy1Pos,
            initialEnemy2Position: enemy2Pos,
            visibility: visibility.Cast<bool>());
    }

    private static Cell ToCell(int cellState) => cellState switch
    {
        0 => Cell.Unknown,
        1 => Cell.Empty,
        2 => Cell.Empty, // Players always stand on empty
        3 => Cell.Wall,
        4 => Cell.Exit,
        5 => Cell.DoorRedClosed,
        6 => Cell.KeyRed,
        7 => Cell.DoorGreenClosed,
        8 => Cell.KeyGreen,
        9 => Cell.DoorBlueClosed,
        10 => Cell.KeyBlue,
        11 => Cell.DoorBlackClosed,
        12 => Cell.PressurePlate,
        13 => Cell.Sword,
        CellStateEnemy => Cell.Empty, // Enemies always stand on empty
        15 => Cell.Health,
        _ => throw new NotImplementedException(),
    };
}
