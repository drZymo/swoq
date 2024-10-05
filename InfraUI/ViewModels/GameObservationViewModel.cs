using Swoq.InfraUI.Models;
using Swoq.Interface;

namespace Swoq.InfraUI.ViewModels;

public class GameObservationViewModel(GameObservation? observation = null) : ViewModelBase
{
    private GameObservation? current = observation;
    private GameObservation? Current
    {
        get => current;
        set
        {
            current = value;
            OnPropertyChanged(nameof(IsLoaded));

            OnPropertyChanged(nameof(UserName));
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

    public string UserName => Current?.UserName ?? "Unknown";
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

    private OverviewViewModel overview = new();
    public OverviewViewModel Overview
    {
        get => overview;
        private set
        {
            overview = value;
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
        Overview = new OverviewViewModel();
        HasPlayer2 = false;
        HasEnemies = false;
        HasPickups = false;
    }

    public void SetGameObservation(GameObservation observation)
    {
        Current = observation;

        Overview.Overview = observation.Overview;

        HasPlayer2 = HasPlayer2 || (observation.Overview.Count(t => t == Tile.Player) > 1);
        HasEnemies = HasEnemies || (observation.Overview.Any(t => t == Tile.Sword || t == Tile.Enemy || t == Tile.Boss));
        HasPickups = HasPickups || (observation.Overview.Any(RequiresInventory));
    }

    public static bool RequiresInventory(Tile tile) => tile switch
    {
        Tile.KeyRed => true,
        Tile.KeyGreen => true,
        Tile.KeyBlue => true,
        Tile.DoorRed => true,
        Tile.DoorGreen => true,
        Tile.DoorBlue => true,
        Tile.Boulder => true,
        _ => false,
    };

    private static string NullableBooleanString(bool? value) => value.HasValue ? (value.Value ? "Yes" : "No") : "Unknown";
}
