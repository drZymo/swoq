using Swoq.Infra;
using Swoq.Server;

namespace Swoq.Test;

[TestFixture]
public class GameTests
{
    private DateTime now;

    [SetUp]
    public void SetUp()
    {
        Clock.Setup(() => now);
    }

    [Test]
    public void ActiveAfterAct()
    {
        var game = new Game(0, TimeSpan.FromSeconds(20));

        Assert.IsFalse(game.IsFinished);
        Assert.IsFalse(game.IsInactive);

        Assert.DoesNotThrow(() => game.Act(new DirectedAction(Server.Action.Move, Direction.East)));

        now += TimeSpan.FromSeconds(10);

        Assert.IsFalse(game.IsFinished);
        Assert.IsFalse(game.IsInactive);

        Assert.DoesNotThrow(() => game.Act(new DirectedAction(Server.Action.Move, Direction.East)));

        Assert.IsFalse(game.IsFinished);
        Assert.IsFalse(game.IsInactive);

        now += TimeSpan.FromSeconds(11);

        Assert.IsFalse(game.IsFinished);
        Assert.IsFalse(game.IsInactive);
    }

    [Test]
    public void InactiveAfterNoAction()
    {
        var game = new Game(0, TimeSpan.FromSeconds(20));

        Assert.IsFalse(game.IsFinished);
        Assert.IsFalse(game.IsInactive);

        Assert.DoesNotThrow(() => game.Act(new DirectedAction(Server.Action.Move, Direction.East)));

        Assert.IsFalse(game.IsFinished);
        Assert.IsFalse(game.IsInactive);

        now += TimeSpan.FromSeconds(21);

        Assert.IsFalse(game.IsFinished);
        Assert.IsTrue(game.IsInactive);

        Assert.Throws<GameFinishedException>(() => game.Act(new DirectedAction(Server.Action.Move, Direction.East)));

        Assert.IsTrue(game.IsFinished);
        Assert.IsTrue(game.IsInactive);
    }
}
