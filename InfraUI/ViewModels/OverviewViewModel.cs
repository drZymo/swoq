using Avalonia.Media;
using Swoq.Infra;
using Position = (int y, int x);

namespace Swoq.InfraUI.ViewModels;

public class OverviewViewModel : ViewModelBase
{
    // Use double buffering
    // Needed for Avalonia, because WriteableBitmaps are not correctly invalidated after unlocking.
    // Parts of the bitmap are not redrawn.
    // By switching images every frame the whole bitmap is redrawn.
    private bool useBitmap1 = true;
    private OverviewImage? bitmap1 = null;
    private OverviewImage? bitmap2 = null;

    public OverviewViewModel() : this(Overview.Empty)
    { }

    public OverviewViewModel(Overview overview)
    {
        Overview = overview;
    }

    private Overview overview = Overview.Empty;
    public Overview Overview
    {
        get => overview;
        set
        {
            overview = value;
            OnPropertyChanged();
            UpdateOverviewImage();
        }
    }

    private IImage? overviewImage = null;
    public IImage? OverviewImage
    {
        get => overviewImage;
        private set
        {
            overviewImage = value;
            OnPropertyChanged();
        }
    }

    private void UpdateOverviewImage()
    {
        if (overview.Height == 0 || overview.Width == 0)
        {
            useBitmap1 = true;
            bitmap1 = null;
            bitmap2 = null;
            OverviewImage = null;
        }
        else
        {
            if (useBitmap1)
            {
                RenderOverview(ref bitmap1);
                OverviewImage = bitmap1?.Image;
            }
            else
            {
                RenderOverview(ref bitmap2);
                OverviewImage = bitmap2?.Image;
            }

            useBitmap1 = !useBitmap1;
        }
    }

    private void RenderOverview(ref OverviewImage? bitmap)
    {
        if (bitmap == null || bitmap.MapHeight != overview.Height || bitmap.MapWidth != overview.Width)
        {
            bitmap = new OverviewImage(overview.Height, overview.Width);
        }

        using var lockedBitmap = bitmap.Lock();

        for (var y = 0; y < overview.Height; y++)
        {
            for (var x = 0; x < overview.Width; x++)
            {
                Position pos = (y, x);
                var tile = overview[pos];
                var isVisible = overview.IsVisible(pos);

                lockedBitmap.Set(y, x, tile, isVisible);
            }
        }
    }
}
