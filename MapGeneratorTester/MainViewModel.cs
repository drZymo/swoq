using Swoq.Infra;
using Swoq.InfraUI.ViewModels;
using System.Windows.Input;

namespace Swoq.MapGeneratorTester;

internal class MainViewModel : ViewModelBase, IDisposable
{
    private static readonly MapGenerator mapGenerator = new();

    public MainViewModel()
    {
        var random = new Random((seed ?? Random.Shared.Next()) + Level);
        Overview.TileMap = mapGenerator.Generate(Level, Height, Width, random).ToOverview();
        Generate = new RelayCommand(HandleGenerate);
    }

    public void Dispose()
    {
        Overview.Dispose();
    }

    public TiledImageViewModel Overview { get; } = new();

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

    public static int MaxLevel => mapGenerator.MaxLevel;

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

    private int height = 48;
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

    private int? seed = null;
    public int? Seed
    {
        get => seed;
        set
        {
            if (seed != value)
            {
                seed = value;
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
        Status = "Generating ...";
        try
        {
            var random = new Random((seed ?? Random.Shared.Next()) + Level);
            Overview.TileMap = mapGenerator.Generate(Level, Height, Width, random).ToOverview();
            Status = "";
        }
        catch
        {
            Overview.TileMap = TileMap.Empty;
            Status = "Generation failed";
        }
    }
}
