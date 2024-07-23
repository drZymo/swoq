using Swoq.Infra;
using Swoq.Server;

namespace Swoq.Test;

[TestFixture]
internal abstract class GameTestBase
{
    protected Game game;
    protected MapCache mapCache;

    protected abstract Map CreateGameMap();

    [SetUp]
    public virtual void SetUp()
    {
        // Make sure all tests are reproducable by fixing the random seed
        Rnd.SetSeed(1337);

        var map = CreateGameMap();
        game = new Game(map, TimeSpan.FromSeconds(20));
        mapCache = new MapCache(map.Height, map.Width, 8); // TODO: Get visiblity range
        mapCache.AddPlayerStates(game.State.Player1, game.State.Player2);
        // Ignore changes of initial state
        mapCache.GetNewChanges();
    }

    protected void Act(
        Server.Action action1, Server.Direction direction1,
        Server.Action? action2 = null, Server.Direction? direction2 = null)
    {
        var directedAction1 = new DirectedAction(action1, direction1);
        var directedAction2 = (action2.HasValue && direction2.HasValue)
            ? new DirectedAction(action2.Value, direction2.Value)
            : null;
        game.Act(directedAction1, directedAction2);
        mapCache.AddPlayerStates(game.State.Player1, game.State.Player2);
    }
}
