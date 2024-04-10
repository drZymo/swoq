using System.Windows.Media;

namespace Viewer.ViewModels;

class CellViewModel : ViewModelBase
{
    public CellViewModel(int[] address, double x, double y, Color color)
    {
        Address = address;
        X = x;
        Y = y;
        Color = color;
    }

    public int[] Address { get; }
    public double X { get; }
    public double Y { get; }

    private Color color = Colors.Black;
    public Color Color
    {
        get => color;
        set
        {
            if (color != value)
            {
                color = value;
                OnPropertyChanged();
            }
        }
    }
}
