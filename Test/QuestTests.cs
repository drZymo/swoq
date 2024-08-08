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

    [SetUp]
    public void SetUp()
    {
        var player = new Player() { Level = 0, Name = "McFly" };
        database.CreatePlayerAsync(player).Wait();
        Assert.That(player.Id, Is.Not.Null);
        playerId = player.Id;

        quest = new Quest(player, database, mapGenerator);
        Assert.That(quest.State.Finished, Is.False);
        Assert.That(quest.State.Level, Is.EqualTo(0));
    }

    [Test]
    public void FinishLevelIncreasesPlayerLevel()
    {
        Assert.That(Player.Level, Is.EqualTo(0));
        quest.Act(DirectedAction.MoveEast);
        Assert.That(Player.Level, Is.EqualTo(1));

        Assert.That(quest.State.Finished, Is.False);
        Assert.That(quest.State.Level, Is.EqualTo(1));
    }

    [Test]
    public void FinishAllLevelsFinishesQuest()
    {
        // Finish 20 levels to reach last one
        Assert.That(Player.Level, Is.EqualTo(0));
        for (var i = 0; i < 20; i++)
        {
            quest.Act(DirectedAction.MoveEast);
        }
        Assert.That(Player.Level, Is.EqualTo(20));
        Assert.That(quest.State.Finished, Is.False);
        Assert.That(quest.State.Level, Is.EqualTo(20));

        // Finish last by fist picking up treasure
        quest.Act(DirectedAction.MoveWest);
        quest.Act(DirectedAction.MoveEast);
        quest.Act(DirectedAction.MoveEast);
        Assert.That(Player.Level, Is.EqualTo(21));

        // Quest finished now
        Assert.That(quest.State.Finished, Is.True);
        Assert.That(quest.State.Level, Is.EqualTo(21));

        // No more actions allowed
        Assert.Throws<GameFinishedException>(() => quest.Act(DirectedAction.MoveEast));
    }

    private Player Player
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
