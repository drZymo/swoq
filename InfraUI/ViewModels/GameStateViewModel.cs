using Swoq.InfraUI.Models;

namespace Swoq.InfraUI.ViewModels;

public class GameStateViewModel(GameState? gameState) : ViewModelBase
{
    private GameState? gameState = gameState;

    public int Level => gameState?.Level ?? -1;
    public string Status => gameState?.Status ?? "Unknown";

    public string Player1Action => gameState?.Player1.LastAction ?? "Unknown";
    public int Player1Health => gameState?.Player1.Health ?? -1;
    public string Player1Inventory => gameState?.Player1.Inventory ?? "Unknown";
    public string Player1HasSword => NullableBooleanString(gameState?.Player1.HasSword);

    public string Player2Action => gameState?.Player2?.LastAction ?? "Unknown";
    public int Player2Health => gameState?.Player2?.Health ?? -1;
    public string Player2Inventory => gameState?.Player2?.Inventory ?? "Unknown";
    public string Player2HasSword => NullableBooleanString(gameState?.Player2?.HasSword);

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

    private bool showHealth = false;
    public bool ShowHealth
    {
        get => showHealth;
        private set
        {
            showHealth = value;
            OnPropertyChanged();
        }
    }

    private bool showInventory = false;
    public bool ShowInventory
    {
        get => showInventory;
        private set
        {
            showInventory = value;
            OnPropertyChanged();
        }
    }

    private bool showSword = false;
    public bool ShowSword
    {
        get => showSword;
        private set
        {
            showSword = value;
            OnPropertyChanged();
        }
    }

    private bool showPlayer2 = false;
    public bool ShowPlayer2
    {
        get => showPlayer2;
        private set
        {
            showPlayer2 = value;
            OnPropertyChanged();
        }
    }

    private static string NullableBooleanString(bool? value) => value.HasValue ? (value.Value ? "Yes" : "No") : "Unknown";

    public void SetGameState(GameState gameState)
    {
        this.gameState = gameState;

        Map = new MapViewModel(gameState.Map);

        OnPropertyChanged(nameof(Level));
        OnPropertyChanged(nameof(Status));

        OnPropertyChanged(nameof(Player1Action));
        OnPropertyChanged(nameof(Player1Health));
        OnPropertyChanged(nameof(Player1Inventory));
        OnPropertyChanged(nameof(Player1HasSword));

        OnPropertyChanged(nameof(Player2Action));
        OnPropertyChanged(nameof(Player2Health));
        OnPropertyChanged(nameof(Player2Inventory));
        OnPropertyChanged(nameof(Player2HasSword));
    }

    public void UpdateMapFeatures(bool hasPickups, bool hasEnemies, bool hasPlayer2)
    {
        ShowHealth = hasEnemies;
        ShowInventory = hasPickups;
        ShowSword = hasEnemies;
        ShowPlayer2 = hasPlayer2;
    }
}
