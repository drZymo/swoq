using Grpc.Net.Client;
using Swoq.Interface;

class GamePlayer : IDisposable
{
    private readonly GrpcChannel channel;
    private readonly GameService.GameServiceClient client;
    private readonly string userId;

    public string? GameId { get; private set; }
    public int? Width { get; private set; }
    public int? Height { get; private set; }
    public int? VisibilityRange { get; private set; }

    public GamePlayer(string userId)
    {
        channel = GrpcChannel.ForAddress("http://localhost:5080");
        client = new GameService.GameServiceClient(channel);
        this.userId = userId;
    }

    public void Dispose()
    {
        channel.Dispose();
    }

    public State Start(int? level)
    {
        var request = new StartRequest() { UserId = userId };
        if (level.HasValue) request.Level = level.Value;

        var response = client.Start(request);
        Console.WriteLine($"Start({level}): {response.Result}");

        GameId = response.HasGameId ? response.GameId : null;
        Width = response.HasWidth ? response.Width : null;
        Height = response.HasHeight ? response.Height : null;
        VisibilityRange = response.HasVisibilityRange ? response.VisibilityRange : null;

        if (response.Result != Result.Ok)
        {
            throw new GamePlayerException() { Result = response.Result };
        }

        return response.State;
    }

    public State Act(DirectedAction action)
    {
        var request = new ActionRequest() { GameId = GameId, Action = action };

        var response = client.Act(request);
        Console.WriteLine($"Act({action}): {response.Result}");

        if (response.Result != Result.Ok)
        {
            throw new GamePlayerException() { Result = response.Result };
        }

        return response.State;
    }
}
