using Swoq.Infra;
using System.Windows.Input;

namespace Viewer.ViewModels;

class MainViewModel : ViewModelBase
{
    public MainViewModel()
    {
        Map = new MapViewModel(MapGenerator.Generate(Level, Width, Height));
        Generate = new RelayCommand(HandleGenerate);
    }

    private MapViewModel map = new();
    public MapViewModel Map
    {
        get => map;
        private set
        {
            map = value;
            OnPropertyChanged();
        }
    }

    private int level = 0;
    public int Level
    {
        get => level;
        set
        {
            if (level != value)
            {
                level = value;
                OnPropertyChanged();
            }
        }
    }

    private int width = 64;
    public int Width
    {
        get => width;
        set
        {
            if (width != value)
            {
                width = value;
                OnPropertyChanged();
            }
        }
    }

    private int height = 64;
    public int Height
    {
        get => height;
        set
        {
            if (height != value)
            {
                height = value;
                OnPropertyChanged();
            }
        }
    }

    public ICommand Generate { get; }

    private void HandleGenerate(object? parameter)
    {
        Map = new MapViewModel(MapGenerator.Generate(Level, Width, Height));
    }
}
