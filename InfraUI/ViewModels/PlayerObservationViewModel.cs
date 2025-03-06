using Swoq.Infra;
using Swoq.InfraUI.Models;
using Swoq.Interface;

namespace Swoq.InfraUI.ViewModels;

public class PlayerObservationViewModel(PlayerObservation? observation = null) : ViewModelBase, IDisposable
{
    private PlayerObservation? observation = observation;

    public PlayerObservation? Observation
    {
        get => observation;
        set
        {
            observation = value;
            OnPropertyChanged();

            OnPropertyChanged(nameof(LastAction));
            OnPropertyChanged(nameof(Health));
            OnPropertyChanged(nameof(HasKeyRed));
            OnPropertyChanged(nameof(HasKeyGreen));
            OnPropertyChanged(nameof(HasKeyBlue));
            OnPropertyChanged(nameof(HasBoulder));
            OnPropertyChanged(nameof(HasTreasure));
            OnPropertyChanged(nameof(HasSword));

            Surroundings.TileMap = Observation?.Surroundings ?? TileMap.Empty;
        }
    }

    public void Dispose()
    {
        Surroundings.Dispose();
    }

    private bool showHealth = false;
    public bool ShowHealth
    {
        get => showHealth;
        set
        {
            if (showHealth != value)
            {
                showHealth = value;
                OnPropertyChanged();
            }
        }
    }

    private bool showSword = false;
    public bool ShowSword
    {
        get => showSword;
        set
        {
            if (showSword != value)
            {
                showSword = value;
                OnPropertyChanged();
            }
        }
    }

    public string LastAction => Observation?.LastAction ?? "Unknown";
    public int Health => Observation?.Health ?? -1;
    public bool HasKeyRed => Observation?.Inventory == Inventory.KeyRed;
    public bool HasKeyGreen => Observation?.Inventory == Inventory.KeyGreen;
    public bool HasKeyBlue => Observation?.Inventory == Inventory.KeyBlue;
    public bool HasBoulder => Observation?.Inventory == Inventory.Boulder;
    public bool HasTreasure => Observation?.Inventory == Inventory.Treasure;
    public bool HasSword => Observation?.HasSword ?? false;

    public TiledImageViewModel Surroundings { get; } = new();
}
