using Avalonia.Threading;
using Grpc.Net.Client;
using Swoq.Infra;
using Swoq.InfraUI.Models;
using Swoq.InfraUI.ViewModels;
using Swoq.Interface;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;

namespace Swoq.QuestDashboard.ViewModels;

internal class GameStateMonitorViewModel : ViewModelBase, IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly Thread readThread;

    public GameStateMonitorViewModel()
    {
        QueuedPlayers = new(queuedPlayers);

        readThread = new Thread(new ThreadStart(MonitorThread));
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

    private readonly ObservableCollection<string> queuedPlayers = [];
    public ReadOnlyObservableCollection<string> QueuedPlayers { get; }

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

    private async void MonitorThread()
    {
        var callOptions = new Grpc.Core.CallOptions(cancellationToken: cancellationTokenSource.Token);
        while (!cancellationTokenSource.IsCancellationRequested)
        {
            try
            {
                bool connected = false;
                Dispatcher.UIThread.Invoke(() => { StatusMessage = "Connecting..."; });
                using var channel = GrpcChannel.ForAddress("http://localhost:5009");
                var client = new Interface.QuestMonitorService.QuestMonitorServiceClient(channel);

                MapBuilder? mapBuilder = null;
                int prevLevel = -1;
                string playerName = "Unknown";

                var call = client.Monitor(new Google.Protobuf.WellKnownTypes.Empty(), callOptions);
                while (await call.ResponseStream.MoveNext(cancellationTokenSource.Token))
                {
                    if (!connected)
                    {
                        Dispatcher.UIThread.Invoke(() => { StatusMessage = "Connected"; });
                        connected = true;
                    }

                    var message = call.ResponseStream.Current;

                    if (message.Started != null)
                    {
                        prevLevel = -1;
                        Dispatcher.UIThread.Invoke(() => { GameState.Reset(); });

                        playerName = message.Started.Player;

                        var response = message.Started.Response;
                        if (mapBuilder == null ||
                            mapBuilder.Height != response.Height ||
                            mapBuilder.Width != response.Width ||
                            mapBuilder.VisibilityRange != response.VisibilityRange)
                        {
                            mapBuilder = new MapBuilder(response.Height, response.Width, response.VisibilityRange);
                        }

                        var gameState = CreateGameState(playerName, response.State, response.Result, ref prevLevel, ref mapBuilder);
                        Dispatcher.UIThread.Invoke(() => { GameState.SetGameState(gameState); });
                    }

                    if (message.Acted != null && mapBuilder != null)
                    {
                        var response = message.Acted.Response;
                        var gameState = CreateGameState(playerName, response.State, response.Result, ref prevLevel, ref mapBuilder, request: message.Acted.Request);
                        Dispatcher.UIThread.Invoke(() => { GameState.SetGameState(gameState); });
                    }

                    if (message.QueueUpdate != null)
                    {
                        var queuedPlayers = message.QueueUpdate.QueuedPlayers.ToImmutableArray();
                        Dispatcher.UIThread.Invoke(() =>
                        {
                            this.queuedPlayers.Clear();
                            foreach (var qp in queuedPlayers)
                            {
                                this.queuedPlayers.Add(qp);
                            }
                        });
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
                if (ex.InnerException is OperationCanceledException)
                {
                    // Stop gracefully on cancel
                    break;
                }
                Dispatcher.UIThread.Invoke(() => { StatusMessage = "Disconnected"; });
                Debug.WriteLine($"Exception {ex.GetType()}: {ex.Message}");
                Thread.Sleep(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Invoke(() => { StatusMessage = "Internal error"; });
                Debug.WriteLine($"Exception {ex.GetType()}: {ex.Message}");
                Thread.Sleep(TimeSpan.FromSeconds(5));
            }
        }
    }

    private static readonly string[] InventoryNames = ["-", "Red key", "Green key", "Blue key"];

    private static (int y, int x) Convert(Position? position) => position == null ? PositionEx.Invalid : (position.Y, position.X);

    private static GameState CreateGameState(
        string playerName,
        Interface.State state,
        Interface.Result actionResult,
        ref int prevLevel,
        ref MapBuilder mapBuilder,
        Interface.ActionRequest? request = null)
    {
        // Clear whole map on new level
        if (state.Level != prevLevel)
        {
            mapBuilder.Reset();
            prevLevel = state.Level;
        }

        mapBuilder.PrepareForNextTimeStep();
        mapBuilder.AddPlayerState(Convert(state.Player1?.Position), state.Player1?.Surroundings ?? [], 1);
        mapBuilder.AddPlayerState(Convert(state.Player2?.Position), state.Player2?.Surroundings ?? [], 2);
        var map = mapBuilder.CreateMap();

        var status = state.Finished ? "Finished" : "Active";

        InfraUI.Models.PlayerState? player1State = null;
        if (state.Player1 != null)
        {
            var action1 = request != null
                ? GetPlayerAction(request.HasAction1 ? request.Action1 : null, request.HasDirection1 ? request.Direction1 : null)
                : "Start";
            player1State = new InfraUI.Models.PlayerState(action1, state.Player1.Health, InventoryNames[state.Player1.Inventory], state.Player1.HasSword);
        }

        InfraUI.Models.PlayerState? player2State = null;
        if (state.Player2 != null)
        {
            var action2 = request != null
                ? GetPlayerAction(request.HasAction2 ? request.Action2 : null, request.HasDirection2 ? request.Direction2 : null)
                : "Start";
            player2State = new InfraUI.Models.PlayerState(action2, state.Player2.Health, InventoryNames[state.Player2.Inventory], state.Player2.HasSword);
        }

        return new GameState(playerName, state.Tick, state.Level, status, actionResult.ConvertToString(), map, player1State, player2State);
    }

    private static string GetPlayerAction(Interface.Action? action, Interface.Direction? direction)
    {
        if (!action.HasValue) return "None";

        var playerAction = new StringBuilder();

        playerAction.Append(action.Value.ConvertToString());
        if (direction.HasValue)
        {
            playerAction.Append(' ');
            playerAction.Append(direction.Value.ConvertToString());
        }
        return playerAction.ToString();
    }
}
