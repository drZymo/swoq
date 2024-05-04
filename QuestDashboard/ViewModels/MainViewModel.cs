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
            using var channel = GrpcChannel.ForAddress("http://localhost:5009");

            var client = new Swoq.Interface.QuestMonitorService.QuestMonitorServiceClient(channel);
            var call = client.Monitor(new Google.Protobuf.WellKnownTypes.Empty());
            while (await call.ResponseStream.MoveNext(cancellationTokenSource.Token))
            {
                Debug.WriteLine(".");
            }
        }
        catch (OperationCanceledException)
        {
            await Console.Out.WriteLineAsync("Stopping ReadAsync");
            // Stop gracefully
        }
    }
}
