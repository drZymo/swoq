using Swoq.Infra;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace MapGeneratorTester.ViewModels;

class MapViewModel : ViewModelBase
{
    private static readonly IImmutableDictionary<Cell, Color> CellColors = new Dictionary<Cell, Color>
    {
        { Cell.Empty, Colors.LightGray },
        { Cell.Wall, Colors.DimGray },
        { Cell.Exit, Colors.Yellow },
        { Cell.DoorRedClosed, Colors.Red },
        { Cell.DoorGreenClosed, Colors.Green },
        { Cell.DoorBlueClosed, Colors.Blue },
        { Cell.DoorBlackClosed, Colors.Black },
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
                if (y == map.InitialPlayerPosition.y && x == map.InitialPlayerPosition.x)
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
