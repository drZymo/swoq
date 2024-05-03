using Swoq.Infra;
using Swoq.Interface;
using System.Collections.Immutable;
using System.IO;
using System.Text;

namespace ReplayViewer.ViewModels;

internal class MainViewModel : ViewModelBase
{
    private static readonly string[] InventoryNames = ["-", "Red key", "Green key", "Blue key"];

    public MainViewModel()
    {
        //using var file = File.OpenRead(@"D:\Projects\swoq-stuff\Replays\Ralph\ff0eaad9-2e05-49fa-8bea-47cf7f602a9f.bin");
        //using var file = File.OpenRead(@"D:\Projects\swoq-stuff\Replays\Ralph\812d2c95-f70f-4eb0-ba93-cf35a6d249ad.bin");
        using var file = File.OpenRead(@"D:\Projects\swoq-stuff\Replays\Ralph\c9174e1d-cad6-4c85-bcfb-1ad244e05c29.bin");

        var startRequest = StartTrainingRequest.Parser.ParseDelimitedFrom(file);
        var startResponse = StartResponse.Parser.ParseDelimitedFrom(file);

        var mapBuilder = new MapBuilder(startResponse.Height, startResponse.Width, startResponse.VisibilityRange);

        var hasPlayer2 = false;
        var hasEnemies = false;
        var hasPickups = false;

        {
            var (hp2, he, hp) = AddTimeStep(ref timeSteps, mapBuilder, null, startResponse.State);
            hasPlayer2 = hasPlayer2 || hp2;
            hasEnemies = hasEnemies || he;
            hasPickups = hasPickups || hp;
        }

        while (file.Position < file.Length)
        {
            var request = ActionRequest.Parser.ParseDelimitedFrom(file);
            var response = ActionResponse.Parser.ParseDelimitedFrom(file);

            var (hp2, he, hp) = AddTimeStep(ref timeSteps, mapBuilder, request, response.State);
            hasPlayer2 = hasPlayer2 || hp2;
            hasEnemies = hasEnemies || he;
            hasPickups = hasPickups || hp;
        }

        HasPlayer2 = hasPlayer2;
        HasEnemies = hasEnemies;
        HasPickups = hasPickups;
        OnTickChanged();
    }

    private static (bool hasPlayer2, bool hasEnemies, bool hasPickups) AddTimeStep(ref IImmutableList<TimeStep> timeSteps, MapBuilder mapBuilder, ActionRequest? request, State state)
    {
        mapBuilder.Hide();
        mapBuilder.AddPlayerState(state.Player1, 1);
        mapBuilder.AddPlayerState(state.Player2, 2);
        var map = mapBuilder.CreateMap();

        var gameState = state.Finished ? "Finished" : "Active";

        var action1 = request != null
            ? GetPlayerAction(request.HasAction1 ? request.Action1 : null, request.HasDirection1 ? request.Direction1 : null)
            : "Start";
        var player1State = new PlayerState(action1, state.Player1);
        var timeStep = new TimeStep(state.Level, gameState, map, player1State);
        timeSteps = timeSteps.Add(timeStep);

        var hasPlayer2 = map.InitialPlayer2Position != null;
        var hasEnemies = map.InitialEnemy1Position != null || map.InitialEnemy2Position != null;
        var hasPickups = map.Any(c => c == Cell.KeyRed || c == Cell.KeyGreen || c == Cell.KeyBlue);
        return (hasPlayer2, hasEnemies, hasPickups);
    }

    private record PlayerState(string LastAction, int Health, string Inventory, bool HasSword)
    {
        public PlayerState(string lastAction, Swoq.Interface.PlayerState state)
            : this(lastAction, state.Health, InventoryNames[state.Inventory], state.HasSword)
        { }
    }

    private record TimeStep(int Level, string GameState, Map Map, PlayerState Player1, PlayerState? Player2 = null);

    private readonly IImmutableList<TimeStep> timeSteps = ImmutableList<TimeStep>.Empty;

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

    public int MaxTick => timeSteps.Count - 1;
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

    private TimeStep? Current => (0 <= tick && tick < timeSteps.Count) ? timeSteps[tick] : null;

    public string Player1Action => Current?.Player1.LastAction ?? "Unknown";
    public int Player1Health => Current?.Player1.Health ?? -1;
    public string Player1Inventory => Current?.Player1.Inventory ?? "Unknown";
    public string Player1HasSword => NullableBooleanString(Current?.Player1.HasSword);

    public string Player2Action => Current?.Player2?.LastAction ?? "Unknown";
    public int Player2Health => Current?.Player2?.Health ?? -1;
    public string Player2Inventory => Current?.Player2?.Inventory ?? "Unknown";
    public string Player2HasSword => NullableBooleanString(Current?.Player2?.HasSword);

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
}
