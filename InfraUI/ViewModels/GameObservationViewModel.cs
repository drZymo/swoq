using Swoq.Infra;
using Swoq.InfraUI.Models;
using Swoq.Interface;

namespace Swoq.InfraUI.ViewModels;

public class GameObservationViewModel(GameObservation? observation = null) : ViewModelBase, IDisposable
{
    private GameObservation? current = observation;
    public GameObservation? Current
    {
        get => current;
        private set
        {
            current = value;
            OnPropertyChanged();

            OnPropertyChanged(nameof(IsLoaded));

            OnPropertyChanged(nameof(UserName));
            OnPropertyChanged(nameof(Tick));
            OnPropertyChanged(nameof(Level));
            OnPropertyChanged(nameof(ActionResult));
            OnPropertyChanged(nameof(IsFinished));
            OnPropertyChanged(nameof(FinishedResult));

            OnPropertyChanged(nameof(HasPlayer2));

            Overview.TileMap = Current?.Overview ?? TileMap.Empty;

            Player1.Observation = Current?.Player1;
            Player1.ShowSword = Current?.HasSwordPickup ?? false;
            Player1.ShowHealth = Current?.HasEnemies ?? false;

            Player2.Observation = Current?.Player2;
            Player2.ShowSword = Current?.HasSwordPickup ?? false;
            Player2.ShowHealth = Current?.HasEnemies ?? false;
        }
    }

    public void Dispose()
    {
        Overview.Dispose();
        Player1.Dispose();
        Player2.Dispose();
    }

    public bool IsLoaded => Current != null;

    public string UserName => Current?.UserName ?? "Unknown";
    public int Tick => Current?.Tick ?? -1;
    public int Level => Current?.Level ?? -1;
    public string ActionResult => Current?.ActionResult ?? "Unknown";
    public bool IsFinished => Current?.Status != GameStatus.Active;
    public string FinishedResult => Current?.Status switch
    {
        null => "",
        GameStatus.Active => "",
        GameStatus.FinishedSuccess => IsQuest ? "🌟 Quest completed 🌟" : "❕ Training completed ❕",
        GameStatus.FinishedTimeout => "🛑 Timeout 🛑",
        GameStatus.FinishedNoProgress => "🛑 No progress 🛑",
        GameStatus.FinishedPlayerDied => "🛑 Player died 🛑",
        GameStatus.FinishedCanceled => "🛑 Game canceled 🛑",
        GameStatus.FinishedPlayer2Died => "🛑 Player 2 died 🛑",
        _ => throw new NotImplementedException(),
    };

    public bool HasPlayer2 => Current?.Player2 != null;

    public TiledImageViewModel Overview { get; } = new();
    public PlayerObservationViewModel Player1 { get; } = new();
    public PlayerObservationViewModel Player2 { get; } = new();

    private bool IsQuest => Current?.IsQuest ?? false;

    public void Reset()
    {
        Current = null;
    }

    public void SetGameObservation(GameObservation observation)
    {
        Current = observation;
    }
}
