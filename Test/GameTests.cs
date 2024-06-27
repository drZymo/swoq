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
        var game = new Game(TestMaps.SquareMap, TimeSpan.FromSeconds(20));

        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.False);
            Assert.That(game.IsInactive, Is.False);
        });

        now += TimeSpan.FromSeconds(10);

        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.False);
            Assert.That(game.IsInactive, Is.False);
        });

        Assert.DoesNotThrow(() => game.Act(new DirectedAction(Server.Action.Move, Direction.East)));

        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.False);
            Assert.That(game.IsInactive, Is.False);
        });

        now += TimeSpan.FromSeconds(9);

        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.False);
            Assert.That(game.IsInactive, Is.False);
        });

        Assert.DoesNotThrow(() => game.Act(new DirectedAction(Server.Action.Move, Direction.East)));

        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.False);
            Assert.That(game.IsInactive, Is.False);
        });

        now += TimeSpan.FromSeconds(2);

        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.False);
            Assert.That(game.IsInactive, Is.False);
        });
    }

    [Test]
    public void InactiveAfterNoAction()
    {
        var game = new Game(TestMaps.SquareMap, TimeSpan.FromSeconds(20));

        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.False);
            Assert.That(game.IsInactive, Is.False);
        });

        Assert.DoesNotThrow(() => game.Act(new DirectedAction(Server.Action.Move, Direction.East)));

        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.False);
            Assert.That(game.IsInactive, Is.False);
        });

        now += TimeSpan.FromSeconds(21);

        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.False);
            Assert.That(game.IsInactive, Is.True);
        });

        Assert.Throws<GameTimeoutException>(() => game.Act(new DirectedAction(Server.Action.Move, Direction.East)));

        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.True);
            Assert.That(game.IsInactive, Is.True);
        });

        Assert.Throws<GameFinishedException>(() => game.Act(new DirectedAction(Server.Action.Move, Direction.East)));

        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.True);
            Assert.That(game.IsInactive, Is.True);
        });
    }
}
