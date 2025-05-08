using Swoq.Infra;
using Swoq.Interface;
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
        var random = new Random(1337);

        var map = CreateGameMap();
        game = new Game(map, TimeSpan.FromSeconds(20), 2000, TimeSpan.FromSeconds(300), random);
        mapCache = new MapCache(map.Height, map.Width, 8); // TODO: Get visiblity range
        mapCache.AddPlayerStates(game.State.Player1, game.State.Player2);
        // Ignore changes of initial state
        mapCache.GetNewChanges();
    }

    protected void Act(DirectedAction? action1 = null, DirectedAction? action2 = null)
    {
        game.Act(action1, action2);
        mapCache.AddPlayerStates(game.State.Player1, game.State.Player2);
    }
}
