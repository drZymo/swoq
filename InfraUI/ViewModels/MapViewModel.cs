using Avalonia.Media;
using Swoq.Infra;

using Position = (int y, int x);

namespace Swoq.InfraUI.ViewModels;

public class MapViewModel : ViewModelBase
{
    // Use double buffering
    // Needed for Avalonia, because WriteableBitmaps are not correctly invalidated after unlocking.
    // Parts of the bitmap are not redrawn.
    // By switching images every frame the whole bitmap is redrawn.
    private bool useBitmap1 = true;
    private MapImage? bitmap1 = null;
    private MapImage? bitmap2 = null;

    public MapViewModel() : this(Map.Empty)
    { }

    public MapViewModel(Map map)
    {
        Map = map;
    }

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

    private IImage? mapImage = null;
    public IImage? MapImage
    {
        get => mapImage;
        private set
        {
            mapImage = value;
            OnPropertyChanged();
        }
    }

    private void UpdateMapImage()
    {
        if (map.Height == 0 || map.Width == 0)
        {
            useBitmap1 = true;
            bitmap1 = null;
            bitmap2 = null;
            MapImage = null;
        }
        else
        {
            if (useBitmap1)
            {
                RenderMap(ref bitmap1);
                MapImage = bitmap1?.Image;
            }
            else
            {
                RenderMap(ref bitmap2);
                MapImage = bitmap2?.Image;
            }

            useBitmap1 = !useBitmap1;
        }
    }

    private void RenderMap(ref MapImage? bitmap)
    {
        if (bitmap == null || bitmap.MapHeight != map.Height || bitmap.MapWidth != map.Width)
        {
            bitmap = new MapImage(map.Height, map.Width);
        }

        using var lockedBitmap = bitmap.Lock();

        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                Position pos = (y, x);
                bool isVisible = map.IsVisible(pos);

                Tile tile;
                if (map.InitialPlayer1Position.Equals(pos) ||
                    (map.InitialPlayer2Position != null && map.InitialPlayer2Position.Equals(pos)))
                {
                    tile = Tile.Player;
                }
                else if ((map.InitialEnemy1Position != null && map.InitialEnemy1Position.Value.Equals(pos)) ||
                    (map.InitialEnemy2Position != null && map.InitialEnemy2Position.Value.Equals(pos)))
                {
                    tile = Tile.Enemy;
                }
                else
                {
                    tile = ToTile(map[pos]);
                }

                lockedBitmap.Set(y, x, tile, isVisible);
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
            Cell.Boulder => Tile.Boulder,
            Cell.PressurePlateWithBoulder => Tile.Boulder,
            _ => Tile.Unknown,
        };
    }
}
