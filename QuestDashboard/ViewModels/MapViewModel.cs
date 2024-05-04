using Swoq.Infra;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QuestDashboard.ViewModels;

internal class MapViewModel : ViewModelBase
{
    private static readonly TileSet tiles;

    static MapViewModel()
    {
        tiles = TileSet.FromImageFile("tiles.png");
    }

    public MapViewModel() : this(Map.Empty)
    { }

    public MapViewModel(Map map)
    {
        Map = map;
    }

    public int Width => map.Width;
    public int Height => map.Height;

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
            mapImage = null;
            OnPropertyChanged(nameof(MapImage));
        }
        else
        {
            if (mapImage == null || mapImage.Height != imgHeight || mapImage.Width != imgWidth)
            {
                mapImage = new WriteableBitmap(imgWidth, imgHeight, 96, 96, PixelFormats.Bgra32, null);
                OnPropertyChanged(nameof(MapImage));
            }

            RenderMap();
        }
    }

    private void RenderMap()
    {
        if (mapImage == null) return;

        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                byte[]? tile = null;

                if (map.InitialPlayer1Position.Equals((y, x)) ||
                    map.InitialPlayer2Position.Equals((y, x)))
                {
                    tile = tiles.Player;
                }
                else if ((map.InitialEnemy1Position != null && map.InitialEnemy1Position.Value.Equals((y, x))) ||
                    (map.InitialEnemy2Position != null && map.InitialEnemy2Position.Value.Equals((y, x))))
                {
                    tile = tiles.Enemy;
                }
                else if (tiles.Cells.TryGetValue(map[y, x], out var cellTile))
                {
                    tile = cellTile;
                }

                if (tile != null)
                {
                    if (!map.IsVisible(y, x))
                    {
                        var newTile = new byte[tile.Length];
                        for (var i = 0; i < tile.Length; i++)
                        {
                            newTile[i] = (byte)(tile[i] / 2);
                        }
                        tile = newTile;
                    }

                    mapImage.WritePixels(new Int32Rect(0, 0, 16, 16), tile, 16 * 4, x * 16, y * 16);
                }
            }
        }
    }
}
