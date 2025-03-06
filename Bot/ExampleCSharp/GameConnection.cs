using Grpc.Net.Client;
using Swoq.Interface;

namespace Bot;

internal class GameConnection : IDisposable
{
    private readonly GrpcChannel channel;
    private readonly GameService.GameServiceClient client;

    public GameConnection()
    {
        channel = GrpcChannel.ForAddress($"http://{Env.Host}:5080");
        client = new(channel);
    }

    public void Dispose()
    {
        channel.Dispose();
    }

    public Game Start(int? level)
    {
        var request = new StartRequest() { UserId = Env.UserId };
        if (level.HasValue) request.Level = level.Value;

        StartResponse? response = null;
        while (true)
        {
            response = client.Start(request);

            if (response.Result == StartResult.Ok) break;

            if (response.Result == StartResult.QuestQueued)
            {
                Console.WriteLine("Quest queued, waiting 2 seconds before retrying ...");
                Thread.Sleep(TimeSpan.FromSeconds(2));
                continue;
            }

            throw new GameException($"Start failed (result {response.Result})");
        }

        var replayFile = new ReplayFile(Env.UserName, request, response);

        return new Game(client, response, replayFile);
    }
}
