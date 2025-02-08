using Swoq.Interface;

namespace Bot;

public class Game(GameService.GameServiceClient client, StartResponse response)
{
    public string GameId { get; } = response.GameId;
    public int MapWidth { get; } = response.Width;
    public int MapHeight { get; } = response.Height;
    public int VisibilityRange { get; } = response.VisibilityRange;
    public State State { get; private set; } = response.State;

    public void Act(DirectedAction action)
    {
        var request = new ActionRequest() { GameId = GameId, Action = action };

        var response = client.Act(request);
        if (response.Result != Result.Ok)
        {
            throw new GameException($"Act failed (result {response.Result})");
        }

        State = response.State;
    }
}
