using Swoq.Infra;
using System.Windows.Input;

namespace MapGeneratorTester.ViewModels;

class MainViewModel : ViewModelBase
{
    public MainViewModel()
    {
        Map = new MapViewModel(MapGenerator.Generate(Level, Height, Width));
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

    private int level = 10;
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

    private int height = 32;
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

    private string status = "";
    public string Status
    {
        get => status;
        private set
        {
            if (status != value)
            {
                status = value;
                OnPropertyChanged();
            }
        }
    }

    public ICommand Generate { get; }

    private void HandleGenerate(object? parameter)
    {
        try
        {
            Status = "Generating ...";
            Map = new MapViewModel(MapGenerator.Generate(Level, Height, Width));
            Status = "";
        }
        catch
        {
            Map = new MapViewModel();
            Status = "Generation failed";
        }
    }
}
