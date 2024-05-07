using Grpc.Net.Client;
using Swoq.InfraUI.Models;
using Swoq.InfraUI.ViewModels;
using System.Diagnostics;
using System.Text;
using System.Windows.Threading;

namespace Swoq.QuestDashboard.ViewModels;

internal class MainViewModel : ViewModelBase, IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly Dispatcher uiDispatcher;
    private readonly Thread readThread;

    public MainViewModel()
    {
        uiDispatcher = Dispatcher.CurrentDispatcher;

        readThread = new Thread(new ThreadStart(ReadThread));
        readThread.Start();
    }

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
        readThread.Join();
    }

    private GameStateViewModel gameState = new(null);
    public GameStateViewModel GameState
    {
        get => gameState;
        private set
        {
            gameState = value;
            OnPropertyChanged();
        }
    }

    private string statusMessage = "";
    public string StatusMessage
    {
        get => statusMessage;
        private set
        {
            statusMessage = value;
            OnPropertyChanged();
        }
    }

    private async void ReadThread()
    {
        while (!cancellationTokenSource.IsCancellationRequested)
        {
            try
            {
                bool connected = false;
                uiDispatcher.Invoke(() => { StatusMessage = "Connecting..."; });
                using var channel = GrpcChannel.ForAddress("http://localhost:5009");

                var client = new Interface.QuestMonitorService.QuestMonitorServiceClient(channel);

                MapBuilder? mapBuilder = null;
                int prevLevel = -1;

                var call = client.Monitor(new Google.Protobuf.WellKnownTypes.Empty());
                while (await call.ResponseStream.MoveNext(cancellationTokenSource.Token))
                {
                    if (!connected)
                    {
                        uiDispatcher.Invoke(() => { StatusMessage = "Connected"; });
                        connected = true;
                    }

                    var message = call.ResponseStream.Current;

                    if (message.Started != null)
                    {

                        prevLevel = -1;
                        uiDispatcher.Invoke(() => { GameState.Reset(); });

                        var response = message.Started.Response;
                        if (mapBuilder == null ||
                            mapBuilder.Height != response.Height ||
                            mapBuilder.Width != response.Width ||
                            mapBuilder.VisibilityRange != response.VisibilityRange)
                        {
                            mapBuilder = new MapBuilder(response.Height, response.Width, response.VisibilityRange);
                        }

                        var gameState = CreateGameState(response.State, ref prevLevel, ref mapBuilder);
                        uiDispatcher.Invoke(() => { GameState.SetGameState(gameState); });
                    }

                    if (message.Acted != null && mapBuilder != null)
                    {
                        var gameState = CreateGameState(message.Acted.Response.State, ref prevLevel, ref mapBuilder, request: message.Acted.Request);
                        uiDispatcher.Invoke(() => { GameState.SetGameState(gameState); });
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Stop gracefully
                break;
            }
            catch (Grpc.Core.RpcException ex)
            {
                uiDispatcher.Invoke(() => { StatusMessage = "Disconnected"; });
                Debug.WriteLine($"Exception {ex.GetType()}: {ex.Message}");
                Thread.Sleep(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                uiDispatcher.Invoke(() => { StatusMessage = "Internal error"; });
                Debug.WriteLine($"Exception {ex.GetType()}: {ex.Message}");
                Thread.Sleep(TimeSpan.FromSeconds(5));
            }
        }
    }

    private static readonly string[] InventoryNames = ["-", "Red key", "Green key", "Blue key"];

    private static GameState CreateGameState(Interface.State state, ref int prevLevel, ref MapBuilder mapBuilder, Interface.ActionRequest? request = null)
    {
        // Clear whole map on new level
        if (state.Level != prevLevel)
        {
            mapBuilder.Reset();
            prevLevel = state.Level;
        }

        mapBuilder.PrepareForNextTimeStep();
        mapBuilder.AddPlayerState(state.Player1, 1);
        mapBuilder.AddPlayerState(state.Player2, 2);
        var map = mapBuilder.CreateMap();

        var status = state.Finished ? "Finished" : "Active";

        var action1 = request != null
            ? GetPlayerAction(request.HasAction1 ? request.Action1 : null, request.HasDirection1 ? request.Direction1 : null)
            : "Start";
        var player1State = new PlayerState(action1, state.Player1.Health, InventoryNames[state.Player1.Inventory], state.Player1.HasSword);

        PlayerState? player2State = null;
        if (state.Player2 != null)
        {
            var action2 = request != null
                ? GetPlayerAction(request.HasAction2 ? request.Action2 : null, request.HasDirection2 ? request.Direction2 : null)
                : "Start";
            player2State = new PlayerState(action2, state.Player2.Health, InventoryNames[state.Player2.Inventory], state.Player2.HasSword);
        }

        return new GameState(state.Tick, state.Level, status, map, player1State, player2State);
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
}
