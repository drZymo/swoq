using Grpc.Net.Client;
using Swoq.Interface;

namespace Bot;

internal class GameConnection : IDisposable
{
    private readonly bool saveReplays;

    private readonly GrpcChannel channel;
    private readonly GameService.GameServiceClient client;

    public GameConnection(bool saveReplays = true)
    {
        this.saveReplays = saveReplays;
        
        channel = GrpcChannel.ForAddress($"http://{Env.Host}:5080");
        client = new(channel);
    }

    public void Dispose()
    {
        channel.Dispose();
    }

    public Game Start(int? level, int? seed = null)
    {
        var request = new StartRequest() { UserId = Env.UserId };
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

            throw new GameException($"Start failed (result {response.Result})");
        }

        ReplayFile? replayFile = saveReplays ? new ReplayFile(Env.UserName, request, response) : null;

        return new Game(client, response, replayFile);
    }
}
