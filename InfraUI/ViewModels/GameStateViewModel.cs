using Swoq.Infra;
using Swoq.InfraUI.Models;

namespace Swoq.InfraUI.ViewModels;

public class GameStateViewModel(GameState? gameState = null) : ViewModelBase
{
    private GameState? current = gameState;
    private GameState? Current
    {
        get => current;
        set
        {
            current = value;
            OnPropertyChanged(nameof(IsLoaded));

            OnPropertyChanged(nameof(PlayerName));
            OnPropertyChanged(nameof(Tick));
            OnPropertyChanged(nameof(Level));
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(ActionResult));

            OnPropertyChanged(nameof(Player1Action));
            OnPropertyChanged(nameof(Player1Health));
            OnPropertyChanged(nameof(Player1Inventory));
            OnPropertyChanged(nameof(Player1HasSword));

            OnPropertyChanged(nameof(Player2Action));
            OnPropertyChanged(nameof(Player2Health));
            OnPropertyChanged(nameof(Player2Inventory));
            OnPropertyChanged(nameof(Player2HasSword));
        }
    }

    public bool IsLoaded => Current != null;

    public string PlayerName => Current?.PlayerName ?? "Unknown";
    public int Tick => Current?.Tick ?? -1;
    public int Level => Current?.Level ?? -1;
    public string Status => Current?.Status ?? "Unknown";
    public string ActionResult => Current?.ActionResult ?? "Unknown";

    public string Player1Action => Current?.Player1?.LastAction ?? "Unknown";
    public int Player1Health => Current?.Player1?.Health ?? -1;
    public string Player1Inventory => Current?.Player1?.Inventory ?? "Unknown";
    public string Player1HasSword => NullableBooleanString(Current?.Player1?.HasSword);

    public string Player2Action => Current?.Player2?.LastAction ?? "Unknown";
    public int Player2Health => Current?.Player2?.Health ?? -1;
    public string Player2Inventory => Current?.Player2?.Inventory ?? "Unknown";
    public string Player2HasSword => NullableBooleanString(Current?.Player2?.HasSword);

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

    private bool hasPlayer2 = false;
    public bool HasPlayer2
    {
        get => hasPlayer2;
        private set
        {
            if (hasPlayer2 != value)
            {
                hasPlayer2 = value;
                OnPropertyChanged();
            }
        }
    }
    private bool hasEnemies = false;
    public bool HasEnemies
    {
        get => hasEnemies;
        private set
        {
            if (hasEnemies != value)
            {
                hasEnemies = value;
                OnPropertyChanged();
            }
        }
    }
    private bool hasPickups = false;
    public bool HasPickups
    {
        get => hasPickups;
        private set
        {
            if (hasPickups != value)
            {
                hasPickups = value;
                OnPropertyChanged();
            }
        }
    }

    public void Reset()
    {
        Current = null;
        Map = new MapViewModel();
        HasPlayer2 = false;
        HasEnemies = false;
        HasPickups = false;
    }

    public void SetGameState(GameState gameState)
    {
        Current = gameState;

        Map.Map = gameState.Map;

        HasPlayer2 = HasPlayer2 || (gameState.Map.InitialPlayer2Position != null);
        HasEnemies = HasEnemies || (gameState.Map.Any(c => c == Cell.Sword) || gameState.Map.InitialEnemy1Position != null || gameState.Map.InitialEnemy2Position != null);
        HasPickups = HasPickups || (gameState.Map.Any(RequiresInventory));
    }

    public static bool RequiresInventory(Cell cell) => cell switch
    {
        Cell.KeyRed => true,
        Cell.KeyGreen => true,
        Cell.KeyBlue => true,
        Cell.DoorRedClosed => true,
        Cell.DoorGreenClosed => true,
        Cell.DoorBlueClosed => true,
        Cell.Boulder => true,
        _ => false,
    };

    private static string NullableBooleanString(bool? value) => value.HasValue ? (value.Value ? "Yes" : "No") : "Unknown";
}
