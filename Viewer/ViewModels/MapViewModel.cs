using Swoq.Infra;
using System.Collections.Immutable;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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
        { Cell.Health, Colors.Goldenrod },
    }.ToImmutableDictionary();

    private static readonly Color PlayerColor = Colors.Magenta;
    private static readonly Color EnemyColor = Colors.Cyan;

    private readonly Map map;

    public MapViewModel() : this(Map.Empty)
    { }

    public MapViewModel(Map map)
    {
        this.map = map;

        byte[] pixels = new byte[map.Height * map.Width * 3];
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var color = CellColors.TryGetValue(map[y, x], out var c) ? c : Colors.Black;
                if (map.InitialPlayer1Position.Equals((y, x)) ||
                    map.InitialPlayer2Position.Equals((y, x)))
                {
                    color = PlayerColor;
                }
                if ((map.InitialEnemy1Position != null && map.InitialEnemy1Position.Value.Equals((y, x))) ||
                    (map.InitialEnemy2Position != null && map.InitialEnemy2Position.Value.Equals((y, x))))
                {
                    color = EnemyColor;
                }

                pixels[(y * map.Width + x) * 3 + 0] = color.B;
                pixels[(y * map.Width + x) * 3 + 1] = color.G;
                pixels[(y * map.Width + x) * 3 + 2] = color.R;
            }
        }

        if (map.Height > 0 && map.Width > 0)
        {
            var bitmap = new WriteableBitmap(map.Width, map.Height, 96, 96, PixelFormats.Bgr24, null);
            bitmap.WritePixels(new Int32Rect(0, 0, map.Width, map.Height), pixels, map.Width * 3, 0, 0);
            mapImage = bitmap;
        }
    }

    public int Width => map.Width;
    public int Height => map.Height;

    private ImageSource? mapImage = null;
    public ImageSource? MapImage
    {
        get => mapImage;
        private set
        {
            mapImage = value;
            OnPropertyChanged();
        }
    }
}
