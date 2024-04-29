using Swoq.Infra;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MapGeneratorTester.ViewModels;

class MapViewModel : ViewModelBase
{
    private readonly Map map;

    #region Tile set

    private static readonly BitmapFrame tileset;
    private static readonly ImmutableDictionary<Cell, byte[]> tiles = ImmutableDictionary<Cell, byte[]>.Empty;
    private static readonly byte[] tilePlayer;
    private static readonly byte[] tileEnemy;

    private static byte[] GetTile(int row, int column)
    {
        byte[] pixels = new byte[16 * 16 * 4];
        tileset.CopyPixels(new Int32Rect(column * 16, row * 16, 16, 16), pixels, 16 * 4, 0);
        return pixels;
    }

    static MapViewModel()
    {
        using (var file = File.OpenRead("tiles.png"))
        {
            var decoder = PngBitmapDecoder.Create(file, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            tileset = decoder.Frames[0];
            tileset.Freeze();
        }

        tiles = tiles.Add(Cell.Wall, GetTile(0, 0));
        tiles = tiles.Add(Cell.Empty, GetTile(0, 1));
        tilePlayer = GetTile(0, 2);
        tileEnemy = GetTile(0, 3);
        tiles = tiles.Add(Cell.Exit, GetTile(0, 4));
        tiles = tiles.Add(Cell.KeyRed, GetTile(1, 0));
        tiles = tiles.Add(Cell.KeyGreen, GetTile(1, 1));
        tiles = tiles.Add(Cell.KeyBlue, GetTile(1, 2));
        tiles = tiles.Add(Cell.PressurePlate, GetTile(1, 3));
        tiles = tiles.Add(Cell.Sword, GetTile(1, 4));
        tiles = tiles.Add(Cell.DoorRedClosed, GetTile(2, 0));
        tiles = tiles.Add(Cell.DoorGreenClosed, GetTile(2, 1));
        tiles = tiles.Add(Cell.DoorBlueClosed, GetTile(2, 2));
        tiles = tiles.Add(Cell.DoorBlackClosed, GetTile(2, 3));
        tiles = tiles.Add(Cell.Health, GetTile(2, 4));
    }

    #endregion

    public MapViewModel() : this(Map.Empty)
    { }


    public MapViewModel(Map map)
    {
        this.map = map;

        var imgHeight = map.Height * 16;
        var imgWidth = map.Width * 16;

        if (imgHeight > 0 && imgWidth > 0)
        {
            var bitmap = new WriteableBitmap(imgWidth, imgHeight, 96, 96, PixelFormats.Bgra32, null);
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    if (map.InitialPlayer1Position.Equals((y, x)) ||
                        map.InitialPlayer2Position.Equals((y, x)))
                    {
                        bitmap.WritePixels(new Int32Rect(0, 0, 16, 16), tilePlayer, 16 * 4, x * 16, y * 16);
                    }
                    else if ((map.InitialEnemy1Position != null && map.InitialEnemy1Position.Value.Equals((y, x))) ||
                        (map.InitialEnemy2Position != null && map.InitialEnemy2Position.Value.Equals((y, x))))
                    {
                        bitmap.WritePixels(new Int32Rect(0, 0, 16, 16), tileEnemy, 16 * 4, x * 16, y * 16);
                    }
                    else if (tiles.TryGetValue(map[y, x], out var tile))
                    {
                        bitmap.WritePixels(new Int32Rect(0, 0, 16, 16), tile, 16 * 4, x * 16, y * 16);
                    }

                }
            }
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
