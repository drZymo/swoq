namespace ReplayViewer.ViewModels;

using System.Windows.Media;
using Position = (int y, int x);

class CellViewModel : ViewModelBase
{
    public CellViewModel(Position position, Color color)
    {
        X = position.x;
        Y = position.y;
        Color = color;
    }

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
