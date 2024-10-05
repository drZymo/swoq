using Avalonia.Media;
using Swoq.Infra;
using Swoq.InfraUI.Models;

namespace Swoq.InfraUI.ViewModels;

public class TiledImageViewModel : ViewModelBase
{
    // Use double buffering
    // Needed for Avalonia, because WriteableBitmaps are not correctly invalidated after unlocking.
    // Parts of the bitmap are not redrawn.
    // By switching images every frame the whole bitmap is redrawn.
    private bool useImage1 = true;
    private TiledImage? image1 = null;
    private TiledImage? image2 = null;

    public TiledImageViewModel() : this(TileMap.Empty)
    { }

    public TiledImageViewModel(TileMap tileMap)
    {
        TileMap = tileMap;
    }

    private TileMap tileMap = TileMap.Empty;
    public TileMap TileMap
    {
        get => tileMap;
        set
        {
            tileMap = value;
            OnPropertyChanged();
            UpdateImage();
        }
    }

    private IImage? image = null;
    public IImage? Image
    {
        get => image;
        private set
        {
            image = value;
            OnPropertyChanged();
        }
    }

    private void UpdateImage()
    {
        if (TileMap.Height == 0 || TileMap.Width == 0)
        {
            useImage1 = true;
            image1 = null;
            image2 = null;
            Image = null;
        }
        else
        {
            if (useImage1)
            {
                Render(ref image1);
                Image = image1?.Image;
            }
            else
            {
                Render(ref image2);
                Image = image2?.Image;
            }

            useImage1 = !useImage1;
        }
    }

    private void Render(ref TiledImage? image)
    {
        if (image == null || image.Height != tileMap.Height || image.Width != tileMap.Width)
        {
            image = new TiledImage(tileMap.Height, tileMap.Width);
        }

        using var lockedBitmap = image.Lock();

        for (var y = 0; y < tileMap.Height; y++)
        {
            var rowStart = y * tileMap.Width;
            for (var x = 0; x < tileMap.Width; x++)
            {
                var tile = tileMap.Tiles[rowStart + x];
                var isVisible = tileMap.Visibility[rowStart + x];

                lockedBitmap.Set(y, x, tile, isVisible);
            }
        }
    }
}
