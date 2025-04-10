using Grpc.Net.Client;
using Swoq.Interface;

namespace Bot;

internal class GameConnection : IDisposable
{
    private readonly string userId;
    private readonly string userName;
    private readonly bool saveReplays;

    private readonly GrpcChannel channel;
    private readonly GameService.GameServiceClient client;

    public GameConnection(string userId, string userName, string host, bool saveReplays = true)
    {
        this.userId = userId;
        this.userName = userName;
        this.saveReplays = saveReplays;

        channel = GrpcChannel.ForAddress($"http://{host}");
        client = new(channel);
    }

    public void Dispose()
    {
        channel.Dispose();
    }

    public Game Start(int? level = null, int? seed = null)
    {
        var request = new StartRequest() { UserId = userId };
        if (level.HasValue) request.Level = level.Value;
        if (seed.HasValue) request.Seed = seed.Value;

        StartResponse? response;
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

            if (response.Result == StartResult.QuestAlreadyActive)
            {
                Console.WriteLine("Quest already active, waiting 2 seconds before retrying ...");
                Thread.Sleep(TimeSpan.FromSeconds(2));
                continue;
            }

            throw new GameException($"Start failed (result {response.Result})");
        }

        ReplayFile? replayFile = saveReplays ? new ReplayFile(userName, request, response) : null;

        return new Game(client, response, replayFile);
    }
}
