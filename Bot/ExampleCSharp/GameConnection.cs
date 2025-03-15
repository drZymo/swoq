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

        channel = GrpcChannel.ForAddress($"http://{DotEnv.Host}");
        client = new(channel);
    }

    public void Dispose()
    {
        channel.Dispose();
    }

    public Game Start()
    {
        var request = new StartRequest() { UserId = DotEnv.UserId };
        Console.WriteLine($"Level: {DotEnv.Level}");
        if (DotEnv.Level.HasValue) request.Level = DotEnv.Level.Value;
        if (DotEnv.Seed.HasValue) request.Seed = DotEnv.Seed.Value;

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

        ReplayFile? replayFile = saveReplays ? new ReplayFile(DotEnv.UserName, request, response) : null;

        return new Game(client, response, replayFile);
    }
}
