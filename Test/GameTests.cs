using Swoq.Infra;
using Swoq.Interface;
using Swoq.Server;

namespace Swoq.Test;

[TestFixture]
public class GameTests
{
    private DateTime now = DateTime.Now;

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
            Assert.That(game.CheckIsActive(), Is.True);
        });

        now += TimeSpan.FromSeconds(10);

        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.False);
            Assert.That(game.CheckIsActive(), Is.True);
        });

        Assert.DoesNotThrow(() => game.Act(DirectedAction.MoveEast));

        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.False);
            Assert.That(game.CheckIsActive(), Is.True);
        });

        now += TimeSpan.FromSeconds(9);

        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.False);
            Assert.That(game.CheckIsActive(), Is.True);
        });

        Assert.DoesNotThrow(() => game.Act(DirectedAction.MoveEast));

        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.False);
            Assert.That(game.CheckIsActive(), Is.True);
        });

        now += TimeSpan.FromSeconds(2);

        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.False);
            Assert.That(game.CheckIsActive(), Is.True);
        });
    }

    [Test]
    public void InactiveAfterNoAction()
    {
        var game = new Game(TestMaps.SquareMap, TimeSpan.FromSeconds(20));

        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.False);
            Assert.That(game.CheckIsActive(), Is.True);
        });

        Assert.DoesNotThrow(() => game.Act(DirectedAction.MoveEast));

        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.False);
            Assert.That(game.CheckIsActive(), Is.True);
        });

        now += TimeSpan.FromSeconds(21);

        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.False);
            Assert.That(game.CheckIsActive(), Is.False);
        });

        Assert.Throws<GameTimeoutException>(() => game.Act(DirectedAction.MoveEast));

        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.True);
            Assert.That(game.CheckIsActive(), Is.False);
        });

        Assert.Throws<GameFinishedException>(() => game.Act(DirectedAction.MoveEast));

        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.True);
            Assert.That(game.CheckIsActive(), Is.False);
        });
    }
}
