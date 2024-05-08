using Swoq.Infra;
using System.Collections.Immutable;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Swoq.InfraUI.ViewModels;

public class MapViewModel : ViewModelBase
{
    private static readonly IImmutableDictionary<Tile, byte[]> tileset;

    static MapViewModel()
    {
        tileset = TileSet.FromImageFile("tiles.png");
    }

    public MapViewModel() : this(Map.Empty)
    { }

    public MapViewModel(Map map)
    {
        Map = map;
    }

    private Tile[,]? tiles = null;
    private bool[,]? visibility = null;
    private WriteableBitmap? mapImage = null;
    public ImageSource? MapImage => mapImage;

    private Map map = Map.Empty;
    public Map Map
    {
        get => map;
        set
        {
            map = value;
            OnPropertyChanged();
            UpdateMapImage();
        }
    }

    private void UpdateMapImage()
    {
        var imgHeight = map.Height * 16;
        var imgWidth = map.Width * 16;

        if (imgHeight == 0 || imgWidth == 0)
        {
            tiles = null;
            visibility = null;
            mapImage = null;
            OnPropertyChanged(nameof(MapImage));
        }
        else
        {
            if (mapImage == null || mapImage.Height != imgHeight || mapImage.Width != imgWidth)
            {
                tiles = new Tile[map.Height, map.Width];
                visibility = new bool[map.Height, map.Width];
                mapImage = new WriteableBitmap(imgWidth, imgHeight, 96, 96, PixelFormats.Bgra32, null);
                OnPropertyChanged(nameof(MapImage));
            }

            RenderMap();
        }
    }

    private void RenderMap()
    {
        if (mapImage == null || tiles == null || visibility == null) return;

        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                (int y, int x) pos = (y, x);
                bool isVisible = map.IsVisible(y, x);

                Tile tile;
                if (map.InitialPlayer1Position.Equals(((int, int))pos) ||
                    map.InitialPlayer2Position.Equals(pos))
                {
                    tile = Tile.Player;
                }
                else if ((map.InitialEnemy1Position != null && map.InitialEnemy1Position.Value.Equals(((int, int))pos)) ||
                    (map.InitialEnemy2Position != null && map.InitialEnemy2Position.Value.Equals(((int, int))pos)))
                {
                    tile = Tile.Enemy;
                }
                else
                {
                    tile = ToTile(map[y, x]);
                }

                if (tile != tiles[y, x] || isVisible != visibility[y, x])
                {
                    byte[] tileImage = tileset[tile];
                    if (!isVisible)
                    {
                        var newTileImage = new byte[tileImage.Length];
                        for (var i = 0; i < tileImage.Length; i++)
                        {
                            newTileImage[i] = (byte)(tileImage[i] * 2 / 3);
                        }
                        tileImage = newTileImage;
                    }

                    mapImage.WritePixels(new Int32Rect(0, 0, 16, 16), tileImage, 16 * 4, x * 16, y * 16);

                    tiles[y, x] = tile;
                    visibility[y, x] = isVisible;
                }
            }
        }
    }

    private static Tile ToTile(Cell cell)
    {
        return cell switch
        {
            Cell.Unknown => Tile.Unknown,
            Cell.Wall => Tile.Wall,
            Cell.Empty => Tile.Empty,
            Cell.Exit => Tile.Exit,
            Cell.KeyRed => Tile.KeyRed,
            Cell.KeyGreen => Tile.KeyGreen,
            Cell.KeyBlue => Tile.KeyBlue,
            Cell.PressurePlate => Tile.PressurePlate,
            Cell.Sword => Tile.Sword,
            Cell.DoorRedClosed => Tile.DoorRed,
            Cell.DoorGreenClosed => Tile.DoorGreen,
            Cell.DoorBlueClosed => Tile.DoorBlue,
            Cell.DoorBlackClosed => Tile.DoorBlack,
            Cell.Health => Tile.Health,
            Cell.DoorRedOpen => Tile.Empty,
            Cell.DoorGreenOpen => Tile.Empty,
            Cell.DoorBlueOpen => Tile.Empty,
            Cell.DoorBlackOpen => Tile.Empty,
            _ => Tile.Unknown,
        };
    }
}
