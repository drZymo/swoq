using Swoq.Infra;
using Swoq.Interface;

namespace ReplayViewer;

using Position = (int y, int x);

internal class MapBuilder(int height, int width, int visibilityRange)
{
    private readonly int height = height;
    private readonly int width = width;
    private readonly int visibilityRange = visibilityRange;
    private readonly Cell[,] mapData = new Cell[height, width];
    private readonly bool[,] visibility = new bool[height, width];

    private Position player1Position = (-1, -1);
    private Position? player2Position = null;

    public void Reset()
    {
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                mapData[y, x] = Cell.Unknown;
                visibility[y, x] = false;
            }
        }
    }

    public void Hide()
    {
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                visibility[y, x] = false;
            }
        }
    }

    public void AddPlayerState(PlayerState? playerState, int playerIndex)
    {
        if (playerIndex < 1 || playerIndex > 2) throw new ArgumentOutOfRangeException(nameof(playerIndex));

        if (playerState == null ||
            (playerState.Position.Y < 0 || playerState.Position.X < 0) ||
            (playerState.Surroundings.Count <= 0))
        {
            if (playerIndex == 1) player1Position = PositionEx.Invalid;
            if (playerIndex == 2) player2Position = null;
            return;
        }

        var top = playerState.Position.Y - visibilityRange;
        var left = playerState.Position.X - visibilityRange;

        var i = 0;
        for (var y = 0; y < visibilityRange * 2 + 1; y++)
        {
            for (var x = 0; x < visibilityRange * 2 + 1; x++)
            {
                var my = top + y;
                var mx = left + x;
                if (0 <= my && my < height && 0 <= mx && mx < width)
                {
                    var cell = ToCell(playerState.Surroundings[i]);
                    if (cell != Cell.Unknown)
                    {
                        mapData[my, mx] = cell;
                        visibility[my, mx] = true;
                    }
                }
                i++;
            }
        }

        if (playerIndex == 1) player1Position = (playerState.Position.Y, playerState.Position.X);
        if (playerIndex == 2) player2Position = (playerState.Position.Y, playerState.Position.X);
    }

    public Map CreateMap()
    {

        // TODO: enemy positions
        return new Map(mapData.Cast<Cell>(), height, width, player1Position,
            initialPlayer2Position: player2Position,
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
        14 => Cell.Empty, // Enemies always stand on empty
        15 => Cell.Health,
        _ => throw new NotImplementedException(),
    };
}
