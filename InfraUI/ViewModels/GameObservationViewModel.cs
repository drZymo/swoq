using Swoq.Infra;
using Swoq.InfraUI.Models;

namespace Swoq.InfraUI.ViewModels;

public class GameObservationViewModel(GameObservation? observation = null) : ViewModelBase
{
    private GameObservation? current = observation;
    public GameObservation? Current
    {
        get => current;
        set
        {
            current = value;
            OnPropertyChanged();

            OnPropertyChanged(nameof(IsLoaded));

            OnPropertyChanged(nameof(UserName));
            OnPropertyChanged(nameof(Tick));
            OnPropertyChanged(nameof(Level));
            OnPropertyChanged(nameof(ActionResult));

            OnPropertyChanged(nameof(HasPlayer2));

            Overview.TileMap = Current?.Overview ?? TileMap.Empty;
            
            Player1.Observation = Current?.Player1;
            Player1.ShowSwordAndHealth = Current?.HasEnemies ?? false;

            Player2.Observation = Current?.Player2;
            Player2.ShowSwordAndHealth = Current?.HasEnemies ?? false;
        }
    }

    public bool IsLoaded => Current != null;

    public string UserName => Current?.UserName ?? "Unknown";
    public int Tick => Current?.Tick ?? -1;
    public int Level => Current?.Level ?? -1;
    public string ActionResult => Current?.ActionResult ?? "Unknown";

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

    public bool HasPlayer2 => Current?.Player2 != null;

    private PlayerObservationViewModel player1 = new();
    public PlayerObservationViewModel Player1
    {
        get => player1;
        private set
        {
            player1 = value;
            OnPropertyChanged();
        }
    }

    private PlayerObservationViewModel player2 = new();
    public PlayerObservationViewModel Player2
    {
        get => player2;
        private set
        {
            player2 = value;
            OnPropertyChanged();
        }
    }

    public void Reset()
    {
        Current = null;
    }

    public void SetGameObservation(GameObservation observation)
    {
        Current = observation;
    }
}
