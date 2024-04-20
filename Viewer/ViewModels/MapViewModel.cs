using Swoq.Infra;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace MapGeneratorTester.ViewModels;

class MapViewModel : ViewModelBase
{
    private static readonly ImmutableDictionary<Cell, Color> CellColors = new Dictionary<Cell, Color>
    {
        { Cell.Empty, Color.FromRgb(64, 64, 64) },
        { Cell.Wall, Color.FromRgb(147, 124, 93) },
        { Cell.Exit, Colors.LightYellow },
        { Cell.DoorRedClosed, Colors.DarkRed },
        { Cell.DoorGreenClosed, Colors.DarkGreen },
        { Cell.DoorBlueClosed, Colors.DarkBlue },
        { Cell.DoorBlackClosed, Colors.Black },
        { Cell.KeyRed, Colors.Red },
        { Cell.KeyGreen, Colors.Green },
        { Cell.KeyBlue, Colors.Blue },
        { Cell.Sword, Colors.Gold },
    }.ToImmutableDictionary();

    private static readonly Color PlayerColor = Colors.Magenta;

    private readonly Map map;

    public MapViewModel()
    {
        this.map = Map.Empty;
        Cells = new ReadOnlyObservableCollection<CellViewModel>(cells);
    }

    public MapViewModel(Map map)
    {
        this.map = map;
        Cells = new ReadOnlyObservableCollection<CellViewModel>(cells);

        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var color = CellColors.TryGetValue(map[y, x], out var c) ? c : Colors.Black;
                if (y == map.InitialPlayer1Position.y && x == map.InitialPlayer1Position.x)
                {
                    color = PlayerColor;
                }
                cells.Add(new CellViewModel((y, x), color));
            }
        }
    }

    public int Width => map.Width;
    public int Height => map.Height;

    private readonly ObservableCollection<CellViewModel> cells = [];
    public ReadOnlyObservableCollection<CellViewModel> Cells { get; private set; }
}
