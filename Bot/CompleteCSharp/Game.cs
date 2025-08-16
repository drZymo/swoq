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
    public int? Seed { get; } = response.HasSeed ? response.Seed : null;

    public ActResult Act(DirectedAction action1, DirectedAction action2)
    {
        var request = new ActRequest() { GameId = GameId, Action = action1, Action2 = action2 };

        var response = client.Act(request);

        replayFile?.Append(request, response);

        //if (response.Result != ActResult.Ok)
        //{
        //    var status = response.State?.Status;
        //    throw new GameException($"Act failed (result {response.Result}, status {status})");
        //}

        if (response.State != null)
        {
            State = response.State;
        }

        return response.Result;
    }
}
