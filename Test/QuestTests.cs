using Swoq.Infra;
using Swoq.Interface;
using Swoq.Server;
using Swoq.Server.Data;

namespace Swoq.Test;

[TestFixture]
internal class QuestTests
{
    private const int MaxLevel = 9;

    private readonly DummyGenerator mapGenerator = new();
    private readonly SwoqDatabaseInMemory database = new();
    private Quest quest;
    private string userId;

    private DateTime now = DateTime.Now;

    [SetUp]
    public void SetUp()
    {
        Clock.Setup(() => now);

        var user = new User() { Level = 0, Name = "McFly" };
        database.CreateUserAsync(user).Wait();
        Assert.That(user.Id, Is.Not.Null);
        userId = user.Id;

        quest = new Quest(user, database, mapGenerator);
        Assert.Multiple(() =>
        {
            Assert.That(quest.State.Finished, Is.False);
            Assert.That(quest.State.Level, Is.EqualTo(0));
        });
    }

    [Test]
    public void FinishLevelIncreasesUserLevel()
    {
        Assert.That(CurrentUser.Level, Is.EqualTo(0));

        now += TimeSpan.FromSeconds(5);
        quest.Act(DirectedAction.MoveEast);
        Assert.Multiple(() =>
        {
            Assert.That(CurrentUser.Level, Is.EqualTo(1));
            Assert.That(quest.State.Finished, Is.False);
            Assert.That(quest.State.Level, Is.EqualTo(1));
        });
    }

    [Test]
    public void FinishAllLevelsFinishesQuest()
    {
        // Finish all levels to reach last one
        Assert.That(CurrentUser.Level, Is.EqualTo(0));
        for (var i = 0; i < MaxLevel; i++)
        {
            now += TimeSpan.FromSeconds(5);
            quest.Act(DirectedAction.MoveEast);
        }

        Assert.Multiple(() =>
        {
            Assert.That(CurrentUser.Level, Is.EqualTo(MaxLevel));
            Assert.That(quest.State.Finished, Is.False);
            Assert.That(quest.State.Level, Is.EqualTo(MaxLevel));
        });

        // Finish last by fist picking up treasure
        quest.Act(DirectedAction.MoveWest);
        quest.Act(DirectedAction.MoveEast);
        quest.Act(DirectedAction.MoveEast);
        Assert.That(CurrentUser.Level, Is.EqualTo(MaxLevel + 1));

        // Quest finished now
        Assert.Multiple(() =>
        {
            Assert.That(quest.State.Finished, Is.True);
            Assert.That(quest.State.Level, Is.EqualTo(MaxLevel + 1));
        });

        // No more actions allowed
        Assert.Throws<GameFinishedException>(() => quest.Act(DirectedAction.MoveEast));
    }


    [Test]
    public void FinishLevelFasterWillUpdateScore()
    {
        // Finish 5 levels in 5*10 seconds, with some dummy actions
        Assert.That(CurrentUser.Level, Is.EqualTo(0));
        for (var i = 0; i < 5; i++)
        {
            now += TimeSpan.FromSeconds(5);
            quest.Act();// dummy action
            now += TimeSpan.FromSeconds(5);
            quest.Act(DirectedAction.MoveEast);
        }

        Assert.Multiple(() =>
        {
            Assert.That(CurrentUser.Level, Is.EqualTo(5));
            Assert.That(CurrentUser.QuestLengthSeconds, Is.EqualTo(50));
            Assert.That(CurrentUser.QuestLengthTicks, Is.EqualTo(10));
            Assert.That(quest.State.Finished, Is.False);
            Assert.That(quest.State.Level, Is.EqualTo(5));
        });

        // Do it faster with same amount of ticks
        {
            // in a new quest
            var newQuest = new Quest(CurrentUser, database, mapGenerator);
            for (var i = 0; i < 5; i++)
            {
                now += TimeSpan.FromSeconds(5);
                newQuest.Act();// dummy action
                newQuest.Act(DirectedAction.MoveEast);
            }

            // Scores should be updated
            Assert.Multiple(() =>
            {
                Assert.That(CurrentUser.Level, Is.EqualTo(5));
                Assert.That(CurrentUser.QuestLengthSeconds, Is.EqualTo(25)); // Faster
                Assert.That(CurrentUser.QuestLengthTicks, Is.EqualTo(10)); // Same
                Assert.That(newQuest.State.Finished, Is.False);
                Assert.That(newQuest.State.Level, Is.EqualTo(5));
            });
        }

        // Do it faster with less ticks
        {
            // in a new quest
            var newQuest = new Quest(CurrentUser, database, mapGenerator);
            for (var i = 0; i < 5; i++)
            {
                now += TimeSpan.FromSeconds(5);
                newQuest.Act(DirectedAction.MoveEast);
            }

            // Scores should be updated
            Assert.Multiple(() =>
            {
                Assert.That(CurrentUser.Level, Is.EqualTo(5));
                Assert.That(CurrentUser.QuestLengthSeconds, Is.EqualTo(25)); // Faster
                Assert.That(CurrentUser.QuestLengthTicks, Is.EqualTo(5)); // Faster
                Assert.That(newQuest.State.Finished, Is.False);
                Assert.That(newQuest.State.Level, Is.EqualTo(5));
            });
        }
    }

    private User CurrentUser
    {
        get
        {
            var user = database.FindUserByIdAsync(userId).Result;
            Assert.That(user, Is.Not.Null);
            return user;
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

            if (level == MaxLevel) map.IsFinal = true;

            return map.ToMap();
        }

        public int MaxLevel { get; } = QuestTests.MaxLevel;
    }
}
