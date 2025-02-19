using Swoq.Infra;
using Swoq.InfraUI.ViewModels;
using System.Windows.Input;

namespace Swoq.MapGeneratorTester;

class MainViewModel : ViewModelBase
{
    private readonly Random random = new();

    public MainViewModel()
    {
        Overview = new TiledImageViewModel(MapGenerator.Generate(Level, Height, Width, random).ToOverview());
        Generate = new RelayCommand(HandleGenerate);
    }

    private TiledImageViewModel overview = new();
    public TiledImageViewModel Overview
    {
        get => overview;
        private set
        {
            overview = value;
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
                HandleGenerate(null);
            }
        }
    }

    public int MaxLevel { get; } = MapGenerator.MaxLevel;

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
                HandleGenerate(null);
            }
        }
    }

    private int height = 38;
    public int Height
    {
        get => height;
        set
        {
            if (height != value)
            {
                height = value;
                OnPropertyChanged();
                HandleGenerate(null);
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
            Overview = new TiledImageViewModel(MapGenerator.Generate(Level, Height, Width, random).ToOverview());
            Status = "";
        }
        catch
        {
            Overview = new TiledImageViewModel();
            Status = "Generation failed";
        }
    }
}
