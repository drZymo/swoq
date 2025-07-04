using Grpc.Net.Client;
using Swoq.Interface;

namespace Bot;

internal class GameConnection : IDisposable
{
    private readonly string userId;
    private readonly string userName;
    private readonly string? replaysFolder;

    private readonly GrpcChannel channel;
    private readonly GameService.GameServiceClient client;

    public GameConnection(string userId, string userName, string host, string? replaysFolder)
    {
        this.userId = userId;
        this.userName = userName;
        this.replaysFolder = replaysFolder;

        channel = GrpcChannel.ForAddress($"http://{host}");
        client = new(channel);
    }

    public void Dispose()
    {
        channel.Dispose();
    }

    public Game Start(int? level = null, int? seed = null)
    {
        var request = new StartRequest() { UserId = userId, UserName = userName };
        if (level.HasValue) request.Level = level.Value;
        if (seed.HasValue) request.Seed = seed.Value;

        var response = client.Start(request);
        while (response.Result == StartResult.QuestQueued)
        {
            Console.WriteLine("Quest queued, retrying ...");
            response = client.Start(request);
        }
        if (response.Result != StartResult.Ok)
        {
            throw new GameException($"Start failed (result {response.Result})");
        }

        ReplayFile? replayFile = string.IsNullOrWhiteSpace(replaysFolder)
            ? null : new ReplayFile(replaysFolder, request, response);

        return new Game(client, response, replayFile);
    }
}
