using Swoq.Data;
using Swoq.Infra;
using Swoq.Interface;
using Swoq.Server;

namespace Swoq.Test;

public class GameServerTests
{
    private DateTime now = DateTime.Now;

    private SwoqDatabaseInMemory database;

    [SetUp]
    public void Setup()
    {
        Clock.Setup(() => now);

        database = new();
    }

    [Test]
    public void UnknownUser()
    {
        var gameServer = new GameServer<DummyGenerator>(database, 1);
        Assert.Throws<UnknownUserException>(() => gameServer.Start("u1", 0));
    }

    [Test]
    public void UserLevelTooLow()
    {
        var gameServer = new GameServer<DummyGenerator>(database, 1);
        GivenUserRegistered();
        Assert.Throws<UserLevelTooLowException>(() => gameServer.Start("u1", 2));
    }

    [Test]
    public void SingleQuestCanStartAndAct()
    {
        var gameServer = new GameServer<DummyGenerator>(database, 1);
        GivenUserRegistered();

        // Start a quest
        GameStartResult? result = null;
        Assert.DoesNotThrow(() => result = gameServer.Start("u1", null));
        Assert.That(result, Is.Not.Null);
        Assert.That(result.UserName, Is.EqualTo("User1"));

        // Act on it
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => gameServer.Act(result.GameId, DirectedAction.MoveSouth));
    }

    [Test]
    public void SingleQuestCanFinish()
    {
        var gameServer = new GameServer<DummyGenerator>(database, 1);
        GivenUserRegistered();

        // Start a quest
        GameStartResult? result = null;
        Assert.DoesNotThrow(() => result = gameServer.Start("u1", null));
        Assert.That(result, Is.Not.Null);
        Assert.That(result.UserName, Is.EqualTo("User1"));

        // Act on it
        GameState state = result.State;
        for (int i = 0; i < 2; i++)
        {
            Assert.That(state.Level, Is.EqualTo(0));
            now += TimeSpan.FromSeconds(1);
            Assert.DoesNotThrow(() => state = gameServer.Act(result.GameId, DirectedAction.MoveSouth));
            now += TimeSpan.FromSeconds(1);
            Assert.DoesNotThrow(() => state = gameServer.Act(result.GameId, DirectedAction.MoveEast));
        }
        Assert.That(state.Level, Is.EqualTo(1));
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => state = gameServer.Act(result.GameId, DirectedAction.MoveSouth));
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => state = gameServer.Act(result.GameId, DirectedAction.MoveEast));
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => state = gameServer.Act(result.GameId, DirectedAction.MoveSouth));

        // Now enter the final exit, game should be finished
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => state = gameServer.Act(result.GameId, DirectedAction.MoveEast));
        Assert.Multiple(() =>
        {
            Assert.That(state.Level, Is.EqualTo(1));
            Assert.That(state.IsFinished, Is.True);
            Assert.That(state.Status, Is.EqualTo(GameStatus.FinishedSuccess));
        });
    }

    [Test]
    public void SecondQuestStartIsQueued()
    {
        var gameServer = new GameServer<DummyGenerator>(database, 1);
        GivenUserRegistered(id: "u1", name: "User1");
        GivenUserRegistered(id: "u2", name: "User2");

        // Start quest for user 1
        GameStartResult? result1 = null;
        Assert.DoesNotThrow(() => result1 = gameServer.Start("u1", null));
        Assert.That(result1, Is.Not.Null);

        // Start quest for user 2, it should be queued
        now += TimeSpan.FromSeconds(1);
        GameStartResult? result2 = null;
        Assert.Throws<QuestQueuedException>(() => result2 = gameServer.Start("u2", null));
        Assert.That(result2, Is.Null);

        // Act on user 1 quest should be possible
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => gameServer.Act(result1.GameId, DirectedAction.MoveSouth));
    }

    [Test]
    public void TwoQuestsCanBeActive()
    {
        var gameServer = new GameServer<DummyGenerator>(database, 2);
        GivenUserRegistered(id: "u1", name: "User1");
        GivenUserRegistered(id: "u2", name: "User2");

        // Start quest for user 1
        GameStartResult? result1 = null;
        Assert.DoesNotThrow(() => result1 = gameServer.Start("u1", null));
        Assert.That(result1, Is.Not.Null);

        // Start quest for user 2
        now += TimeSpan.FromSeconds(1);
        GameStartResult? result2 = null;
        Assert.DoesNotThrow(() => result2 = gameServer.Start("u2", null));
        Assert.That(result2, Is.Not.Null);

        // Act on user 1 quest should be possible
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => gameServer.Act(result1.GameId, DirectedAction.MoveSouth));

        // Act on user 2 quest should be possible
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => gameServer.Act(result2.GameId, DirectedAction.MoveEast));
    }

    [Test]
    public void TimeoutOnQuestWillFinishItAndAllowsNextInQueueToStart()
    {
        var gameServer = new GameServer<DummyGenerator>(database, 1);
        GivenUserRegistered(id: "u1", name: "User1");
        GivenUserRegistered(id: "u2", name: "User2");

        // Start quest for user 1 and 2
        GameStartResult? result1 = null;
        Assert.DoesNotThrow(() => result1 = gameServer.Start("u1", null));
        Assert.That(result1, Is.Not.Null);
        now += TimeSpan.FromSeconds(1);
        Assert.Throws<QuestQueuedException>(() => gameServer.Start("u2", null));

        // Let both users keep responding for a while
        now += TimeSpan.FromSeconds(4);
        Assert.DoesNotThrow(() => gameServer.Act(result1.GameId, DirectedAction.MoveSouth));
        Assert.Throws<QuestQueuedException>(() => gameServer.Start("u2", null));
        now += TimeSpan.FromSeconds(4);
        Assert.DoesNotThrow(() => gameServer.Act(result1.GameId, DirectedAction.MoveNorth));
        Assert.Throws<QuestQueuedException>(() => gameServer.Start("u2", null));

        // Stop responding with user 1 so it times out but keep responding with user 2
        now += TimeSpan.FromSeconds(4);
        Assert.Throws<QuestQueuedException>(() => gameServer.Start("u2", null));
        now += TimeSpan.FromSeconds(4);
        Assert.Throws<QuestQueuedException>(() => gameServer.Start("u2", null));

        // User 1 quest is inactive and was stopped
        // User 2 is now first in line and can start the quest
        now += TimeSpan.FromSeconds(4);
        GameStartResult? result2 = null;
        Assert.DoesNotThrow(() => result2 = gameServer.Start("u2", null));
        Assert.That(result2, Is.Not.Null);

        // Acting on 1 should now fail on timeout
        Assert.Throws<GameFinishedException>(() => gameServer.Act(result1.GameId, DirectedAction.MoveSouth));
    }

    [Test]
    public void UserIsRemovedFromQuestQueueWhenItStopsCallingStart()
    {
        var gameServer = new GameServer<DummyGenerator>(database, 1);
        GivenUserRegistered(id: "u1", name: "User1");
        GivenUserRegistered(id: "u2", name: "User2");
        GivenUserRegistered(id: "p3", name: "User3");

        // Start quest for user 1, 2 and 3
        GameStartResult? result1 = null;
        Assert.DoesNotThrow(() => result1 = gameServer.Start("u1", null));
        Assert.That(result1, Is.Not.Null);
        now += TimeSpan.FromSeconds(1);
        Assert.Throws<QuestQueuedException>(() => gameServer.Start("u2", null));
        now += TimeSpan.FromSeconds(1);
        Assert.Throws<QuestQueuedException>(() => gameServer.Start("p3", null));

        // Let user 1 and 3 keep responding for a while
        now += TimeSpan.FromSeconds(4);
        Assert.DoesNotThrow(() => gameServer.Act(result1.GameId, DirectedAction.MoveSouth));
        Assert.Throws<QuestQueuedException>(() => gameServer.Start("p3", null));
        now += TimeSpan.FromSeconds(4);
        Assert.DoesNotThrow(() => gameServer.Act(result1.GameId, DirectedAction.MoveNorth));
        Assert.Throws<QuestQueuedException>(() => gameServer.Start("p3", null));

        // Stop responding with user 1 so it times out but keep responding with user 3
        now += TimeSpan.FromSeconds(4);
        Assert.Throws<QuestQueuedException>(() => gameServer.Start("p3", null));
        now += TimeSpan.FromSeconds(4);
        Assert.Throws<QuestQueuedException>(() => gameServer.Start("p3", null));

        // User 1 quest is inactive and was stopped
        // User 2 has been removed from the queue
        // User 3 is now first in line and can start the quest
        now += TimeSpan.FromSeconds(4);
        GameStartResult? result2 = null;
        Assert.DoesNotThrow(() => result2 = gameServer.Start("p3", null));
        Assert.That(result2, Is.Not.Null);
    }

    [Test]
    public void OldGamesAreCleanedUpAfterAWhile()
    {
        var gameServer = new GameServer<DummyGenerator>(database, 1);
        GivenUserRegistered(id: "u1", name: "User1");
        GivenUserRegistered(id: "u2", name: "User2");

        // Start quest for user 1 and let it timeout
        GameStartResult? result1 = null;
        Assert.DoesNotThrow(() => result1 = gameServer.Start("u1", null));
        Assert.That(result1, Is.Not.Null);
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => gameServer.Act(result1.GameId, DirectedAction.MoveSouth));
        now += TimeSpan.FromSeconds(20);
        Assert.Throws<GameFinishedException>(() => gameServer.Act(result1.GameId, DirectedAction.MoveNorth));

        // Start training for user 2 and let it timeout
        GameStartResult? result2 = null;
        Assert.DoesNotThrow(() => result2 = gameServer.Start("u2", 0));
        Assert.That(result2, Is.Not.Null);
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => gameServer.Act(result2.GameId, DirectedAction.MoveEast));
        now += TimeSpan.FromSeconds(70);
        Assert.Throws<GameFinishedException>(() => gameServer.Act(result2.GameId, DirectedAction.MoveWest));

        // Wait a while
        now += TimeSpan.FromMinutes(11);

        // Start a new training
        GameStartResult? result3 = null;
        Assert.DoesNotThrow(() => result3 = gameServer.Start("u1", 0));
        Assert.That(result3, Is.Not.Null);

        // Oldest two games should now be removed
        Assert.Throws<UnknownGameIdException>(() => gameServer.Act(result1.GameId, DirectedAction.MoveNorth));
        Assert.Throws<UnknownGameIdException>(() => gameServer.Act(result2.GameId, DirectedAction.MoveWest));
    }

    [Test]
    public void ThirdQuestIsQueued()
    {
        var gameServer = new GameServer<DummyGenerator>(database, 2);
        GivenUserRegistered(id: "u1", name: "User1");
        GivenUserRegistered(id: "u2", name: "User2");
        GivenUserRegistered(id: "u3", name: "User3");

        // Start quest for user 1
        GameStartResult? result1 = null;
        Assert.DoesNotThrow(() => result1 = gameServer.Start("u1", null));
        Assert.That(result1, Is.Not.Null);

        // Start quest for user 2
        now += TimeSpan.FromSeconds(1);
        GameStartResult? result2 = null;
        Assert.DoesNotThrow(() => result2 = gameServer.Start("u2", null));
        Assert.That(result2, Is.Not.Null);

        // Start quest for user 3, should be queued
        now += TimeSpan.FromSeconds(1);
        GameStartResult? result3 = null;
        Assert.Throws<QuestQueuedException>(() => result3 = gameServer.Start("u3", null));
        Assert.That(result3, Is.Null);

        // Act on user 1 quest should be possible
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => gameServer.Act(result1.GameId, DirectedAction.MoveSouth));

        // Act on user 2 quest should be possible
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => gameServer.Act(result2.GameId, DirectedAction.MoveEast));
    }

    [Test]
    public void ThirdQuestBecomesActiveAfterFirstFinishes()
    {
        var gameServer = new GameServer<DummyGenerator>(database, 2);
        GivenUserRegistered(id: "u1", name: "User1");
        GivenUserRegistered(id: "u2", name: "User2");
        GivenUserRegistered(id: "u3", name: "User3");

        // Start quest for user 1
        GameStartResult? result1 = null;
        Assert.DoesNotThrow(() => result1 = gameServer.Start("u1", null));
        Assert.That(result1, Is.Not.Null);

        // Start quest for user 2
        now += TimeSpan.FromSeconds(1);
        GameStartResult? result2 = null;
        Assert.DoesNotThrow(() => result2 = gameServer.Start("u2", null));
        Assert.That(result2, Is.Not.Null);

        // Start quest for user 3, should be queued
        now += TimeSpan.FromSeconds(1);
        GameStartResult? result3 = null;
        Assert.Throws<QuestQueuedException>(() => result3 = gameServer.Start("u3", null));
        Assert.That(result3, Is.Null);

        // Act on user 1 quest should be possible
        GameState state = result1.State;
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => state = gameServer.Act(result1.GameId, DirectedAction.MoveSouth));

        // Act on user 2 quest should be possible
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => gameServer.Act(result2.GameId, DirectedAction.MoveEast));

        // 3 is still queued
        Assert.Throws<QuestQueuedException>(() => result3 = gameServer.Start("u3", null));

        // Finish the game with 1 and keep moving back-and-forth with 2
        Assert.That(state.Level, Is.EqualTo(0));
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => state = gameServer.Act(result1.GameId, DirectedAction.MoveEast));
        Assert.DoesNotThrow(() => gameServer.Act(result2.GameId, DirectedAction.MoveWest));
        Assert.Throws<QuestQueuedException>(() => result3 = gameServer.Start("u3", null));
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => state = gameServer.Act(result1.GameId, DirectedAction.MoveSouth));
        Assert.DoesNotThrow(() => gameServer.Act(result2.GameId, DirectedAction.MoveEast));
        Assert.Throws<QuestQueuedException>(() => result3 = gameServer.Start("u3", null));
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => state = gameServer.Act(result1.GameId, DirectedAction.MoveEast));
        Assert.DoesNotThrow(() => gameServer.Act(result2.GameId, DirectedAction.MoveWest));
        Assert.Throws<QuestQueuedException>(() => result3 = gameServer.Start("u3", null));
        Assert.That(state.Level, Is.EqualTo(1));
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => state = gameServer.Act(result1.GameId, DirectedAction.MoveSouth));
        Assert.DoesNotThrow(() => gameServer.Act(result2.GameId, DirectedAction.MoveEast));
        Assert.Throws<QuestQueuedException>(() => result3 = gameServer.Start("u3", null));
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => state = gameServer.Act(result1.GameId, DirectedAction.MoveEast));
        Assert.DoesNotThrow(() => gameServer.Act(result2.GameId, DirectedAction.MoveWest));
        Assert.Throws<QuestQueuedException>(() => result3 = gameServer.Start("u3", null));
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => state = gameServer.Act(result1.GameId, DirectedAction.MoveSouth));
        Assert.DoesNotThrow(() => gameServer.Act(result2.GameId, DirectedAction.MoveEast));
        Assert.Throws<QuestQueuedException>(() => result3 = gameServer.Start("u3", null));

        // Now enter the final exit, game should be finished
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => state = gameServer.Act(result1.GameId, DirectedAction.MoveEast));
        Assert.DoesNotThrow(() => gameServer.Act(result2.GameId, DirectedAction.MoveWest));
        Assert.Multiple(() =>
        {
            Assert.That(state.Level, Is.EqualTo(1));
            Assert.That(state.IsFinished, Is.True);
            Assert.That(state.Status, Is.EqualTo(GameStatus.FinishedSuccess));
        });

        // Now, since game 1 is finished, user 3 can continue
        Assert.DoesNotThrow(() => result3 = gameServer.Start("u3", null));
        // User 1 cannot act anymore
        Assert.Throws<GameFinishedException>(() => state = gameServer.Act(result1.GameId, DirectedAction.MoveEast));
        // User 2 can still act
        Assert.DoesNotThrow(() => gameServer.Act(result2.GameId, DirectedAction.MoveEast));
    }

    [Test]
    public void ActiveUserCannotQueueAgain()
    {
        var gameServer = new GameServer<DummyGenerator>(database, 1);
        GivenUserRegistered(id: "u1", name: "User1");
        GivenUserRegistered(id: "u2", name: "User2");
        GivenUserRegistered(id: "u3", name: "User3");

        // Start quest for user 1
        GameStartResult? result1 = null;
        Assert.DoesNotThrow(() => result1 = gameServer.Start("u1", null));
        Assert.That(result1, Is.Not.Null);

        // Start quest for user 2, should be queued
        now += TimeSpan.FromSeconds(1);
        GameStartResult? result2 = null;
        Assert.Throws<QuestQueuedException>(() => result2 = gameServer.Start("u2", null));
        Assert.That(result2, Is.Null);

        // Start another quest for user 1, should not be allowed
        now += TimeSpan.FromSeconds(1);
        GameStartResult? result3 = null;
        Assert.Throws<QuestAlreadyActiveException>(() => result3 = gameServer.Start("u1", null));
        Assert.That(result3, Is.Null);

        // Act on user 1 quest should be possible
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => gameServer.Act(result1.GameId, DirectedAction.MoveSouth));
    }

    #region Primitives

    private void GivenUserRegistered(string id = "u1", string name = "User1", int level = 1)
    {
        database.CreateUserAsync(new User { Id = id, Name = name, Level = level }).Wait();
    }

    #endregion

    private class DummyGenerator : IMapGenerator
    {
        public static Map Generate(int level, int height, int width)
        {
            width = 5;
            height = 5;
            MutableMap map = new(level, 5, 5);
            for (var x = 0; x < width; x++)
            {
                map[0, x] = Cell.Wall;
                map[height - 1, x] = Cell.Wall;
            }
            for (var y = 1; y < height - 1; y++)
            {
                map[y, 0] = Cell.Wall;
                map[y, width - 1] = Cell.Wall;
            }

            for (var y = 1; y < height - 1; y++)
            {
                for (var x = 1; x < width - 1; x++)
                {
                    map[y, x] = Cell.Empty;
                }
            }

            map[height - 2, width - 2] = Cell.Exit;

            map.Player1.Position = (1, 1);

            if (level == MaxLevel)
            {
                map[height - 2, width - 3] = Cell.Treasure;
                map.IsFinal = true;
            }

            return map.ToMap();
        }

        public static int MaxLevel { get; } = 1;
    }
}