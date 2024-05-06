using Grpc.Net.Client;
using System.Diagnostics;

namespace QuestDashboard.ViewModels;

internal class MainViewModel : ViewModelBase, IDisposable
{
    private CancellationTokenSource cancellationTokenSource = new();
    private Thread readThread;
    private ReplayViewModel replay;

    public MainViewModel()
    {
        replay = new ReplayViewModel(@"D:\Projects\swoq-stuff\Replays\Training\Ralph\level 9 - d11b7860-2fc2-4d5e-b369-dae05f2c4512.bin");

        readThread = new Thread(new ThreadStart(ReadThread));
        readThread.Start();
    }

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
        readThread.Join();
    }

    public ReplayViewModel Replay
    {
        get => replay;
        private set
        {
            replay = value;
            OnPropertyChanged();
        }
    }

    private async void ReadThread()
    {
        try
        {
            MapBuilder? mapBuilder = null;

            using var channel = GrpcChannel.ForAddress("http://localhost:5009");

            var client = new Swoq.Interface.QuestMonitorService.QuestMonitorServiceClient(channel);
            var call = client.Monitor(new Google.Protobuf.WellKnownTypes.Empty());
            while (await call.ResponseStream.MoveNext(cancellationTokenSource.Token))
            {
                var message = call.ResponseStream.Current;

                if (message.Started != null)
                {
                    Debug.WriteLine("Started");
                    var response = message.Started.Response;
                    if (mapBuilder == null ||
                        mapBuilder.Height != response.Height ||
                        mapBuilder.Width != response.Width ||
                        mapBuilder.VisibilityRange != response.VisibilityRange)
                    {
                        mapBuilder = new MapBuilder(response.Height, response.Width, response.VisibilityRange);
                    }

                    mapBuilder.PrepareForNextTimeStep();
                    mapBuilder.AddPlayerState(response.State.Player1, 1);
                    mapBuilder.AddPlayerState(response.State.Player2, 2);
                    mapBuilder.CreateMap();
                }

                if (message.Acted != null)
                {
                    Debug.WriteLine("Acted");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Stop gracefully
        }
    }
}
