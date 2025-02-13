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
        Assert.That(Assert.Throws<GameServerException>(() => gameServer.Start("u1", 0)).Result, Is.EqualTo(Result.UnknownUser));
    }

    [Test]
    public void UserLevelTooLow()
    {
        var gameServer = new GameServer<DummyGenerator>(database, 1);
        GivenUserRegistered();
        Assert.That(Assert.Throws<GameServerException>(() => gameServer.Start("u1", 2)).Result, Is.EqualTo(Result.UserLevelTooLow));
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
        Assert.That(Assert.Throws<GameServerException>(() => result2 = gameServer.Start("u2", null)).Result, Is.EqualTo(Result.QuestQueued));
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
        Assert.That(Assert.Throws<GameServerException>(() => gameServer.Start("u2", null)).Result, Is.EqualTo(Result.QuestQueued));

        // Let both users keep responding for a while
        now += TimeSpan.FromSeconds(4);
        Assert.DoesNotThrow(() => gameServer.Act(result1.GameId, DirectedAction.MoveSouth));
        Assert.That(Assert.Throws<GameServerException>(() => gameServer.Start("u2", null)).Result, Is.EqualTo(Result.QuestQueued));
        now += TimeSpan.FromSeconds(4);
        Assert.DoesNotThrow(() => gameServer.Act(result1.GameId, DirectedAction.MoveNorth));
        Assert.That(Assert.Throws<GameServerException>(() => gameServer.Start("u2", null)).Result, Is.EqualTo(Result.QuestQueued));

        // Stop responding with user 1 so it times out but keep responding with user 2
        now += TimeSpan.FromSeconds(4);
        Assert.That(Assert.Throws<GameServerException>(() => gameServer.Start("u2", null)).Result, Is.EqualTo(Result.QuestQueued));
        now += TimeSpan.FromSeconds(4);
        Assert.That(Assert.Throws<GameServerException>(() => gameServer.Start("u2", null)).Result, Is.EqualTo(Result.QuestQueued));

        // User 1 quest is inactive and was stopped
        // User 2 is now first in line and can start the quest
        now += TimeSpan.FromSeconds(4);
        GameStartResult? result2 = null;
        Assert.DoesNotThrow(() => result2 = gameServer.Start("u2", null));
        Assert.That(result2, Is.Not.Null);

        // Acting on 1 should now fail on timeout
        Assert.That(Assert.Throws<GameServerException>(() => gameServer.Act(result1.GameId, DirectedAction.MoveSouth)).Result, Is.EqualTo(Result.GameFinished));
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
        Assert.That(Assert.Throws<GameServerException>(() => gameServer.Start("u2", null)).Result, Is.EqualTo(Result.QuestQueued));
        now += TimeSpan.FromSeconds(1);
        Assert.That(Assert.Throws<GameServerException>(() => gameServer.Start("p3", null)).Result, Is.EqualTo(Result.QuestQueued));

        // Let user 1 and 3 keep responding for a while
        now += TimeSpan.FromSeconds(4);
        Assert.DoesNotThrow(() => gameServer.Act(result1.GameId, DirectedAction.MoveSouth));
        Assert.That(Assert.Throws<GameServerException>(() => gameServer.Start("p3", null)).Result, Is.EqualTo(Result.QuestQueued));
        now += TimeSpan.FromSeconds(4);
        Assert.DoesNotThrow(() => gameServer.Act(result1.GameId, DirectedAction.MoveNorth));
        Assert.That(Assert.Throws<GameServerException>(() => gameServer.Start("p3", null)).Result, Is.EqualTo(Result.QuestQueued));

        // Stop responding with user 1 so it times out but keep responding with user 3
        now += TimeSpan.FromSeconds(4);
        Assert.That(Assert.Throws<GameServerException>(() => gameServer.Start("p3", null)).Result, Is.EqualTo(Result.QuestQueued));
        now += TimeSpan.FromSeconds(4);
        Assert.That(Assert.Throws<GameServerException>(() => gameServer.Start("p3", null)).Result, Is.EqualTo(Result.QuestQueued));

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
        Assert.That(Assert.Throws<GameServerException>(() => gameServer.Act(result1.GameId, DirectedAction.MoveNorth)).Result, Is.EqualTo(Result.GameFinished));

        // Start training for user 2 and let it timeout
        GameStartResult? result2 = null;
        Assert.DoesNotThrow(() => result2 = gameServer.Start("u2", 0));
        Assert.That(result2, Is.Not.Null);
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => gameServer.Act(result2.GameId, DirectedAction.MoveEast));
        now += TimeSpan.FromSeconds(70);
        Assert.That(Assert.Throws<GameServerException>(() => gameServer.Act(result2.GameId, DirectedAction.MoveWest)).Result, Is.EqualTo(Result.GameFinished));

        // Wait a while
        now += TimeSpan.FromMinutes(11);

        // Start a new training
        GameStartResult? result3 = null;
        Assert.DoesNotThrow(() => result3 = gameServer.Start("u1", 0));
        Assert.That(result3, Is.Not.Null);

        // Oldest two games should now be removed
        Assert.That(Assert.Throws<GameServerException>(() => gameServer.Act(result1.GameId, DirectedAction.MoveNorth)).Result, Is.EqualTo(Result.UnknownGameId));
        Assert.That(Assert.Throws<GameServerException>(() => gameServer.Act(result2.GameId, DirectedAction.MoveWest)).Result, Is.EqualTo(Result.UnknownGameId));
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
        Assert.That(Assert.Throws<GameServerException>(() => result3 = gameServer.Start("u3", null)).Result, Is.EqualTo(Result.QuestQueued));
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
        Assert.That(Assert.Throws<GameServerException>(() => result3 = gameServer.Start("u3", null)).Result, Is.EqualTo(Result.QuestQueued));
        Assert.That(result3, Is.Null);

        // Act on user 1 quest should be possible
        GameState state = result1.State;
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => state = gameServer.Act(result1.GameId, DirectedAction.MoveSouth));

        // Act on user 2 quest should be possible
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => gameServer.Act(result2.GameId, DirectedAction.MoveEast));

        // 3 is still queued
        Assert.That(Assert.Throws<GameServerException>(() => result3 = gameServer.Start("u3", null)).Result, Is.EqualTo(Result.QuestQueued));

        // Finish the game with 1 and keep moving back-and-forth with 2
        Assert.That(state.Level, Is.EqualTo(0));
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => state = gameServer.Act(result1.GameId, DirectedAction.MoveEast));
        Assert.DoesNotThrow(() => gameServer.Act(result2.GameId, DirectedAction.MoveWest));
        Assert.That(Assert.Throws<GameServerException>(() => result3 = gameServer.Start("u3", null)).Result, Is.EqualTo(Result.QuestQueued));
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => state = gameServer.Act(result1.GameId, DirectedAction.MoveSouth));
        Assert.DoesNotThrow(() => gameServer.Act(result2.GameId, DirectedAction.MoveEast));
        Assert.That(Assert.Throws<GameServerException>(() => result3 = gameServer.Start("u3", null)).Result, Is.EqualTo(Result.QuestQueued));
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => state = gameServer.Act(result1.GameId, DirectedAction.MoveEast));
        Assert.DoesNotThrow(() => gameServer.Act(result2.GameId, DirectedAction.MoveWest));
        Assert.That(Assert.Throws<GameServerException>(() => result3 = gameServer.Start("u3", null)).Result, Is.EqualTo(Result.QuestQueued));
        Assert.That(state.Level, Is.EqualTo(1));
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => state = gameServer.Act(result1.GameId, DirectedAction.MoveSouth));
        Assert.DoesNotThrow(() => gameServer.Act(result2.GameId, DirectedAction.MoveEast));
        Assert.That(Assert.Throws<GameServerException>(() => result3 = gameServer.Start("u3", null)).Result, Is.EqualTo(Result.QuestQueued));
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => state = gameServer.Act(result1.GameId, DirectedAction.MoveEast));
        Assert.DoesNotThrow(() => gameServer.Act(result2.GameId, DirectedAction.MoveWest));
        Assert.That(Assert.Throws<GameServerException>(() => result3 = gameServer.Start("u3", null)).Result, Is.EqualTo(Result.QuestQueued));
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => state = gameServer.Act(result1.GameId, DirectedAction.MoveSouth));
        Assert.DoesNotThrow(() => gameServer.Act(result2.GameId, DirectedAction.MoveEast));
        Assert.That(Assert.Throws<GameServerException>(() => result3 = gameServer.Start("u3", null)).Result, Is.EqualTo(Result.QuestQueued));

        // Now enter the final exit, game should be finished
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => state = gameServer.Act(result1.GameId, DirectedAction.MoveEast));
        Assert.DoesNotThrow(() => gameServer.Act(result2.GameId, DirectedAction.MoveWest));
        Assert.Multiple(() =>
        {
            Assert.That(state.Level, Is.EqualTo(1));
            Assert.That(state.Status, Is.EqualTo(GameStatus.FinishedSuccess));
        });

        // Now, since game 1 is finished, user 3 can continue
        Assert.DoesNotThrow(() => result3 = gameServer.Start("u3", null));
        // User 1 cannot act anymore
        Assert.That(Assert.Throws<GameServerException>(() => state = gameServer.Act(result1.GameId, DirectedAction.MoveEast)).Result, Is.EqualTo(Result.GameFinished));
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
        Assert.That(Assert.Throws<GameServerException>(() => result2 = gameServer.Start("u2", null)).Result, Is.EqualTo(Result.QuestQueued));
        Assert.That(result2, Is.Null);

        // Start another quest for user 1, should not be allowed
        now += TimeSpan.FromSeconds(1);
        GameStartResult? result3 = null;
        Assert.That(Assert.Throws<GameServerException>(() => result3 = gameServer.Start("u1", null)).Result, Is.EqualTo(Result.QuestAlreadyActive));
        Assert.That(result3, Is.Null);

        // Act on user 1 quest should be possible
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => gameServer.Act(result1.GameId, DirectedAction.MoveSouth));
    }

    [Test]
    public void RandomSeedIsStableOverTime()
    {
        GivenUserRegistered(id: "u1", name: "One", level: 23);
        var gameServer = new GameServer<MapGenerator>(database);

        // These actions have been recorded by playing with the Python bot and recording all actions that resulted in 'OK' response.
        // The GameServer class was adapted to always use random seed 42 before each map was generated.
        var level = 20;
        (int a1, int a2)[] actions = [(3, 3), (2, 0), (3, 2), (3, 2), (3, 1), (1, 0), (2, 0), (2, 0), (2, 0), (2, 0), (2, 3), (2, 2), (2, 2), (2, 2), (1, 1), (0, 3), (0, 3), (0, 0), (0, 3), (0, 3), (0, 3), (0, 3), (0, 4), (0, 4), (0, 3), (0, 3), (3, 3), (3, 3), (3, 3), (2, 3), (2, 3), (2, 3), (4, 3), (4, 3), (4, 3), (3, 3), (3, 0), (3, 3), (3, 3), (3, 3), (4, 3), (3, 3), (3, 3), (3, 3), (3, 3), (2, 3), (2, 3), (2, 3), (2, 3), (2, 3), (2, 2), (2, 3), (2, 2), (2, 0), (2, 0), (2, 2), (3, 2), (2, 2), (2, 2), (3, 3), (3, 2), (2, 2), (2, 3), (2, 2), (2, 2), (2, 2), (1, 2), (1, 2), (1, 1), (2, 2), (2, 2), (2, 1), (2, 1), (3, 1), (3, 0), (3, 2), (2, 3), (3, 2), (3, 0), (3, 2), (3, 3), (3, 3), (2, 3), (2, 0), (2, 3), (3, 0), (4, 3), (4, 3), (4, 3), (4, 3), (3, 3), (3, 3), (4, 3), (3, 3), (4, 3), (4, 3), (4, 3), (4, 2), (4, 2), (4, 3), (4, 3), (4, 3), (4, 3), (4, 2), (4, 2), (4, 2), (4, 2), (2, 2), (2, 2), (2, 0), (2, 2), (2, 2), (2, 3), (1, 2), (1, 2), (1, 2), (2, 2), (2, 3), (2, 3), (2, 3), (2, 3), (2, 3), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 1), (1, 1), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 3), (2, 2), (3, 1), (2, 1), (3, 1), (3, 1), (3, 1), (3, 1), (3, 1), (4, 1), (4, 1), (4, 1), (2, 2), (2, 1), (2, 1), (2, 1), (1, 4), (2, 4), (2, 4), (2, 4), (2, 4), (2, 1), (2, 4), (2, 1), (2, 1), (2, 1), (2, 1), (1, 1), (3, 1), (2, 0), (2, 1), (1, 1), (1, 2), (1, 2), (1, 2), (1, 2), (1, 0), (1, 2), (1, 3), (1, 2), (1, 3), (1, 3), (1, 3), (1, 3), (1, 3), (1, 3), (4, 3), (4, 3), (4, 3), (4, 0), (4, 3), (4, 3), (4, 3), (4, 3), (4, 3), (4, 3), (4, 3), (4, 3), (4, 3), (4, 3), (1, 3), (1, 3), (1, 4), (1, 4), (1, 4), (4, 4), (4, 4), (1, 4), (1, 3), (2, 4), (1, 4), (1, 4), (2, 4), (2, 4), (2, 0), (2, 4), (2, 4), (2, 1), (2, 1), (1, 1), (3, 3), (3, 3), (3, 4), (4, 3), (4, 3), (4, 2), (4, 1), (4, 2), (4, 2), (4, 2), (4, 2), (3, 2), (3, 1), (3, 1), (3, 2), (3, 2), (3, 2), (3, 2), (2, 2), (2, 2), (2, 1), (2, 0), (6, 1), (6, 0), (6, 1), (6, 1), (4, 1), (4, 1), (1, 1), (1, 1), (1, 1), (1, 2), (1, 2), (4, 1), (4, 1), (1, 1), (1, 4), (1, 4), (1, 4), (1, 4), (3, 1), (4, 4), (4, 4), (3, 4), (4, 1), (4, 3), (4, 3), (4, 2), (4, 2), (4, 2), (4, 2), (4, 2), (4, 2), (4, 2), (4, 3), (4, 3), (4, 3), (4, 3), (4, 3), (4, 3), (4, 3), (4, 3), (1, 3), (3, 3), (6, 3), (6, 3), (1, 4), (3, 4), (2, 4), (3, 8), (2, 8), (2, 8), (2, 2), (3, 1), (3, 1), (3, 1), (2, 1), (2, 1), (3, 0), (3, 1), (3, 1), (3, 1), (3, 1), (3, 2), (3, 2), (4, 1), (4, 1), (4, 1), (4, 4), (4, 4), (4, 4), (1, 1), (4, 2), (4, 7), (4, 7), (4, 7), (4, 4), (4, 1), (4, 1), (4, 1), (4, 2), (4, 2), (1, 2), (4, 1), (4, 1), (4, 1), (1, 1), (1, 1), (1, 1), (1, 2), (1, 2), (1, 2), (1, 6), (1, 1), (1, 2), (4, 2), (4, 2), (8, 2), (2, 2), (3, 2), (3, 2), (3, 1), (3, 3), (3, 3), (3, 0), (3, 1), (3, 1), (3, 1), (3, 1), (3, 1), (2, 1), (2, 1), (2, 4), (2, 4), (2, 4), (2, 4), (2, 4), (2, 4), (2, 4), (2, 1), (2, 4), (2, 4), (3, 4), (3, 4), (2, 4), (2, 1), (2, 1), (3, 1), (3, 4), (3, 4), (3, 4), (3, 4), (3, 4), (2, 4), (2, 4), (2, 4), (2, 4), (2, 4), (2, 4), (2, 4), (2, 2), (2, 2), (1, 2), (2, 4), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 0), (2, 3), (3, 3), (2, 3), (3, 1), (2, 3), (3, 3), (3, 3), (3, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 3), (3, 3), (3, 0), (3, 0), (3, 1), (3, 0), (3, 3), (3, 2), (3, 3), (2, 3), (3, 3), (4, 3), (3, 3), (3, 3), (4, 3), (4, 3), (8, 3), (8, 4), (8, 4), (8, 4), (8, 5), (4, 0), (4, 4), (2, 3), (2, 3), (2, 3), (1, 3), (1, 2), (1, 2), (1, 2), (4, 2), (4, 2), (4, 2), (4, 7), (2, 3), (3, 2), (3, 3), (2, 0), (2, 0), (2, 0), (3, 0), (3, 0), (3, 0), (3, 0), (3, 0), (3, 0), (3, 0), (3, 0), (2, 0), (2, 0), (2, 0), (2, 0), (3, 0)];

        // Set the seed and start a game
        Rnd.SetSeed(42);
        GameStartResult? startResult = null;
        Assert.DoesNotThrow(() => startResult = gameServer.Start("u1", level));
        Assert.That(startResult, Is.Not.Null);

        // All actions should be reproduceable without throwing
        GameState? state = null;
        foreach (var (a1, a2) in actions)
        {
            state = gameServer.Act(startResult.GameId, a1 != 0 ? (DirectedAction)a1 : null, a2 != 0 ? (DirectedAction)a2 : null);
        }
        Assert.That(state, Is.Not.Null);
        Assert.That(state.Status, Is.EqualTo(GameStatus.FinishedSuccess));
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

            map.Player1.Position = map.Pos(1, 1);

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
