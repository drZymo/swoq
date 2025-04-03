using Swoq.Interface;

namespace Bot;

internal class Game(GameService.GameServiceClient client, StartResponse response, ReplayFile? replayFile) : IDisposable
{
    public void Dispose()
    {
        replayFile?.Dispose();
    }

    public string GameId { get; } = response.GameId;
    public int MapWidth { get; } = response.MapWidth;
    public int MapHeight { get; } = response.MapHeight;
    public int VisibilityRange { get; } = response.VisibilityRange;
    public State State { get; private set; } = response.State;

    public void Act(DirectedAction action)
    {
        var request = new ActRequest() { GameId = GameId, Action = action };

        var response = client.Act(request);

        replayFile?.Append(request, response);

        if (response.Result != ActResult.Ok)
        {
            throw new GameException($"Act failed (result {response.Result})");
        }

        State = response.State;
    }
}
