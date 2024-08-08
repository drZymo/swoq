using Swoq.Infra;
using Swoq.Interface;
using Swoq.Server;
using Swoq.Server.Data;

namespace Swoq.Test;

[TestFixture]
internal class QuestTests
{
    private readonly DummyGenerator mapGenerator = new();
    private readonly SwoqDatabaseInMemory database = new();
    private Quest quest;
    private string playerId;

    private DateTime now = DateTime.Now;

    [SetUp]
    public void SetUp()
    {
        Clock.Setup(() => now);

        var player = new Player() { Level = 0, Name = "McFly" };
        database.CreatePlayerAsync(player).Wait();
        Assert.That(player.Id, Is.Not.Null);
        playerId = player.Id;

        quest = new Quest(player, database, mapGenerator);
        Assert.Multiple(() =>
        {
            Assert.That(quest.State.Finished, Is.False);
            Assert.That(quest.State.Level, Is.EqualTo(0));
        });
    }

    [Test]
    public void FinishLevelIncreasesPlayerLevel()
    {
        Assert.That(CurrentPlayer.Level, Is.EqualTo(0));

        now += TimeSpan.FromSeconds(5);
        quest.Act(DirectedAction.MoveEast);
        Assert.Multiple(() =>
        {
            Assert.That(CurrentPlayer.Level, Is.EqualTo(1));
            Assert.That(quest.State.Finished, Is.False);
            Assert.That(quest.State.Level, Is.EqualTo(1));
        });
    }

    [Test]
    public void FinishAllLevelsFinishesQuest()
    {
        // Finish 20 levels to reach last one
        Assert.That(CurrentPlayer.Level, Is.EqualTo(0));
        for (var i = 0; i < 20; i++)
        {
            now += TimeSpan.FromSeconds(5);
            quest.Act(DirectedAction.MoveEast);
        }

        Assert.Multiple(() =>
        {
            Assert.That(CurrentPlayer.Level, Is.EqualTo(20));
            Assert.That(quest.State.Finished, Is.False);
            Assert.That(quest.State.Level, Is.EqualTo(20));
        });

        // Finish last by fist picking up treasure
        quest.Act(DirectedAction.MoveWest);
        quest.Act(DirectedAction.MoveEast);
        quest.Act(DirectedAction.MoveEast);
        Assert.That(CurrentPlayer.Level, Is.EqualTo(21));

        // Quest finished now
        Assert.Multiple(() =>
        {
            Assert.That(quest.State.Finished, Is.True);
            Assert.That(quest.State.Level, Is.EqualTo(21));
        });

        // No more actions allowed
        Assert.Throws<GameFinishedException>(() => quest.Act(DirectedAction.MoveEast));
    }


    [Test]
    public void FinishLevelFasterWillUpdateScore()
    {
        // Finish 5 levels in 5*10 seconds, with some dummy actions
        Assert.That(CurrentPlayer.Level, Is.EqualTo(0));
        for (var i = 0; i < 5; i++)
        {
            now += TimeSpan.FromSeconds(5);
            quest.Act();// dummy action
            now += TimeSpan.FromSeconds(5);
            quest.Act(DirectedAction.MoveEast);
        }

        Assert.Multiple(() =>
        {
            Assert.That(CurrentPlayer.Level, Is.EqualTo(5));
            Assert.That(CurrentPlayer.QuestLengthSeconds, Is.EqualTo(50));
            Assert.That(CurrentPlayer.QuestLengthTicks, Is.EqualTo(10));
            Assert.That(quest.State.Finished, Is.False);
            Assert.That(quest.State.Level, Is.EqualTo(5));
        });

        // Do it faster with same amount of ticks
        {
            // in a new quest
            var newQuest = new Quest(CurrentPlayer, database, mapGenerator);
            for (var i = 0; i < 5; i++)
            {
                now += TimeSpan.FromSeconds(5);
                newQuest.Act();// dummy action
                newQuest.Act(DirectedAction.MoveEast);
            }

            // Scores should be updated
            Assert.Multiple(() =>
            {
                Assert.That(CurrentPlayer.Level, Is.EqualTo(5));
                Assert.That(CurrentPlayer.QuestLengthSeconds, Is.EqualTo(25)); // Faster
                Assert.That(CurrentPlayer.QuestLengthTicks, Is.EqualTo(10)); // Same
                Assert.That(newQuest.State.Finished, Is.False);
                Assert.That(newQuest.State.Level, Is.EqualTo(5));
            });
        }

        // Do it faster with less ticks
        {
            // in a new quest
            var newQuest = new Quest(CurrentPlayer, database, mapGenerator);
            for (var i = 0; i < 5; i++)
            {
                now += TimeSpan.FromSeconds(5);
                newQuest.Act(DirectedAction.MoveEast);
            }

            // Scores should be updated
            Assert.Multiple(() =>
            {
                Assert.That(CurrentPlayer.Level, Is.EqualTo(5));
                Assert.That(CurrentPlayer.QuestLengthSeconds, Is.EqualTo(25)); // Faster
                Assert.That(CurrentPlayer.QuestLengthTicks, Is.EqualTo(5)); // Faster
                Assert.That(newQuest.State.Finished, Is.False);
                Assert.That(newQuest.State.Level, Is.EqualTo(5));
            });
        }
    }

    private Player CurrentPlayer
    {
        get
        {
            var player = database.FindPlayerByIdAsync(playerId).Result;
            Assert.That(player, Is.Not.Null);
            return player;
        }
    }

    private class DummyGenerator : IMapGenerator
    {
        public Map Generate(int level)
        {
            MutableMap map = new(level, 3, 5);
            for (var x = 0; x < 5; x++)
            {
                map[0, x] = Cell.Wall;
                map[2, x] = Cell.Wall;
            }
            map[1, 0] = Cell.Wall;
            map[1, 1] = Cell.Treasure;
            map[1, 2] = Cell.Empty;
            map[1, 3] = Cell.Exit;
            map[1, 4] = Cell.Wall;

            map.Player1.Position = (1, 2);
            return map.ToMap();
        }
    }
}
