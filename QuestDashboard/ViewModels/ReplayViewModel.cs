using Swoq.Infra;
using Swoq.InfraUI.ViewModels;
using Swoq.Interface;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Windows.Input;
using System.Windows.Threading;

namespace Swoq.QuestDashboard.ViewModels;

internal class ReplayViewModel : ViewModelBase
{
    public record PlayerState(string LastAction, int Health, string Inventory, bool HasSword)
    {
        internal PlayerState(string lastAction, Swoq.Interface.PlayerState state)
            : this(lastAction, state.Health, InventoryNames[state.Inventory], state.HasSword)
        { }
    }

    public record TimeStep(int Level, string GameState, Map Map, PlayerState Player1, PlayerState? Player2 = null);

    private static readonly string[] InventoryNames = ["-", "Red key", "Green key", "Blue key"];

    public ReplayViewModel(string path)
    {
        using var file = File.OpenRead(path);

        var startRequest = StartRequest.Parser.ParseDelimitedFrom(file);
        var startResponse = StartResponse.Parser.ParseDelimitedFrom(file);

        var mapBuilder = new MapBuilder(startResponse.Height, startResponse.Width, startResponse.VisibilityRange);

        {
            var (hp2, he, hp) = AddTimeStep(ref timeSteps, mapBuilder, null, startResponse.State);
            HasPlayer2 = HasPlayer2 || hp2;
            HasEnemies = HasEnemies || he;
            HasPickups = HasPickups || hp;
        }

        while (file.Position < file.Length)
        {
            var request = ActionRequest.Parser.ParseDelimitedFrom(file);
            var response = ActionResponse.Parser.ParseDelimitedFrom(file);

            var (hp2, he, hp) = AddTimeStep(ref timeSteps, mapBuilder, request, response.State);
            HasPlayer2 = HasPlayer2 || hp2;
            HasEnemies = HasEnemies || he;
            HasPickups = HasPickups || hp;
        }

        PlayPauseCommand = new RelayCommand(PlayPause);

        OnTickChanged();
    }

    private readonly IImmutableList<TimeStep> timeSteps = ImmutableList<TimeStep>.Empty;
    public IImmutableList<TimeStep> TimeSteps => timeSteps;

    private int tick = 0;
    public int Tick
    {
        get => tick;
        set
        {
            if (tick != value)
            {
                tick = Math.Clamp(value, 0, MaxTick);
                OnPropertyChanged();
                OnTickChanged();
            }
        }
    }

    private TimeStep? Current => (0 <= tick && tick < timeSteps.Count) ? timeSteps[tick] : null;

    public int MaxTick => TimeSteps.Count - 1;
    public int Level => Current?.Level ?? -1;
    public string GameState => Current?.GameState ?? "Unknown";

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

    public bool HasPlayer2 { get; } = false;
    public bool HasEnemies { get; } = false;
    public bool HasPickups { get; } = false;

    public string Player1Action => Current?.Player1.LastAction ?? "Unknown";
    public int Player1Health => Current?.Player1.Health ?? -1;
    public string Player1Inventory => Current?.Player1.Inventory ?? "Unknown";
    public string Player1HasSword => NullableBooleanString(Current?.Player1.HasSword);

    public string Player2Action => Current?.Player2?.LastAction ?? "Unknown";
    public int Player2Health => Current?.Player2?.Health ?? -1;
    public string Player2Inventory => Current?.Player2?.Inventory ?? "Unknown";
    public string Player2HasSword => NullableBooleanString(Current?.Player2?.HasSword);


    public ICommand PlayPauseCommand { get; }

    private DispatcherTimer? timer = null;


    private static (bool hasPlayer2, bool hasEnemies, bool hasPickups) AddTimeStep(ref IImmutableList<TimeStep> timeSteps, MapBuilder mapBuilder, ActionRequest? request, State state)
    {
        // Clear whole map on new level
        if (timeSteps.Count > 0 && state.Level != timeSteps[^1].Level)
        {
            mapBuilder.Reset();
        }

        mapBuilder.PrepareForNextTimeStep();
        mapBuilder.AddPlayerState(state.Player1, 1);
        mapBuilder.AddPlayerState(state.Player2, 2);
        var map = mapBuilder.CreateMap();

        var gameState = state.Finished ? "Finished" : "Active";

        var action1 = request != null
            ? GetPlayerAction(request.HasAction1 ? request.Action1 : null, request.HasDirection1 ? request.Direction1 : null)
            : "Start";
        var player1State = new PlayerState(action1, state.Player1);

        PlayerState? player2State = null;
        if (state.Player2 != null)
        {
            var action2 = request != null
                ? GetPlayerAction(request.HasAction2 ? request.Action2 : null, request.HasDirection2 ? request.Direction2 : null)
                : "Start";
            player2State = new PlayerState(action2, state.Player2);
        }

        var timeStep = new TimeStep(state.Level, gameState, map, player1State, player2State);
        timeSteps = timeSteps.Add(timeStep);

        var hasPlayer2 = map.InitialPlayer2Position != null;
        var hasEnemies = map.InitialEnemy1Position != null || map.InitialEnemy2Position != null;
        var hasPickups = map.Any(c => c == Cell.KeyRed || c == Cell.KeyGreen || c == Cell.KeyBlue);
        return (hasPlayer2, hasEnemies, hasPickups);
    }

    private void OnTickChanged()
    {
        var step = timeSteps[tick];

        Map = new MapViewModel(step.Map);

        OnPropertyChanged(nameof(Level));
        OnPropertyChanged(nameof(GameState));

        OnPropertyChanged(nameof(Player1Action));
        OnPropertyChanged(nameof(Player1Health));
        OnPropertyChanged(nameof(Player1Inventory));
        OnPropertyChanged(nameof(Player1HasSword));

        OnPropertyChanged(nameof(Player2Action));
        OnPropertyChanged(nameof(Player2Health));
        OnPropertyChanged(nameof(Player2Inventory));
        OnPropertyChanged(nameof(Player2HasSword));
    }

    private static string GetPlayerAction(Swoq.Interface.Action? action, Swoq.Interface.Direction? direction)
    {
        if (!action.HasValue) return "None";

        var playerAction = new StringBuilder();
        playerAction.Append(action.Value.ToString());
        if (direction.HasValue)
        {
            playerAction.Append(' ');
            playerAction.Append(direction.Value.ToString());
        }
        return playerAction.ToString();
    }

    private static string NullableBooleanString(bool? value) => value.HasValue ? (value.Value ? "Yes" : "No") : "Unknown";

    private void PlayPause(object? _)
    {
        if (timer == null)
        {
            timer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Normal, OnTimer, Dispatcher.CurrentDispatcher);
        }
        else
        {
            timer.Stop();
            timer = null;
        }
    }
    private void OnTimer(object? sender, EventArgs e)
    {
        Tick++;
    }
}
