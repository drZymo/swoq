using Swoq.Infra;
using Swoq.Interface;
using Swoq.Server;
using Swoq.Server.Data;

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
        var gameServer = new GameServer(database, 1);
    Assert.Throws<UnknownUserException>(() => gameServer.Start("u1", 0));
    }

    [Test]
    public void UserLevelTooLow()
    {
        var gameServer = new GameServer(database, 1);
        GivenUserRegistered();
        Assert.Throws<UserLevelTooLowException>(() => gameServer.Start("u1", 2));
    }

    [Test]
    public void SingleQuestCanStartAndAct()
    {
        var gameServer = new GameServer(database, 1);
        GivenUserRegistered();

        // Start a quest
        GameServer.StartResult? result = null;
        Assert.DoesNotThrow(() => result = gameServer.Start("u1", null));
        Assert.That(result, Is.Not.Null);
        Assert.That(result.UserName, Is.EqualTo("User1"));

        // Act on it
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => gameServer.Act(result.GameId, DirectedAction.MoveSouth));
    }

    [Test]
    public void SecondQuestStartIsQueued()
    {
        var gameServer = new GameServer(database, 1);
        GivenUserRegistered(id: "u1", name: "User1");
        GivenUserRegistered(id: "u2", name: "User2");

        // Start quest for user 1
        GameServer.StartResult? result1 = null;
        Assert.DoesNotThrow(() => result1 = gameServer.Start("u1", null));
        Assert.That(result1, Is.Not.Null);

        // Start quest for user 2, it should be queued
        now += TimeSpan.FromSeconds(1);
        GameServer.StartResult? result2 = null;
        Assert.Throws<QuestQueuedException>(() => result2 = gameServer.Start("u2", null));
        Assert.That(result2, Is.Null);

        // Act on user 1 quest should be possible
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => gameServer.Act(result1.GameId, DirectedAction.MoveSouth));
    }

    [Test]
    public void TwoQuestsCanBeActive()
    {
        var gameServer = new GameServer(database, 2);
        GivenUserRegistered(id: "u1", name: "User1");
        GivenUserRegistered(id: "u2", name: "User2");

        // Start quest for user 1
        GameServer.StartResult? result1 = null;
        Assert.DoesNotThrow(() => result1 = gameServer.Start("u1", null));
        Assert.That(result1, Is.Not.Null);

        // Start quest for user 2
        now += TimeSpan.FromSeconds(1);
        GameServer.StartResult? result2 = null;
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
        var gameServer = new GameServer(database, 1);
        GivenUserRegistered(id: "u1", name: "User1");
        GivenUserRegistered(id: "u2", name: "User2");

        // Start quest for user 1 and 2
        GameServer.StartResult? result1 = null;
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
        GameServer.StartResult? result2 = null;
        Assert.DoesNotThrow(() => result2 = gameServer.Start("u2", null));
        Assert.That(result2, Is.Not.Null);

        // Acting on 1 should now fail on timeout
        Assert.Throws<NoProgressException>(() => gameServer.Act(result1.GameId, DirectedAction.MoveSouth));
    }

    [Test]
    public void UserIsRemovedFromQuestQueueWhenItStopsCallingStart()
    {
        var gameServer = new GameServer(database, 1);
        GivenUserRegistered(id: "u1", name: "User1");
        GivenUserRegistered(id: "u2", name: "User2");
        GivenUserRegistered(id: "p3", name: "User3");

        // Start quest for user 1, 2 and 3
        GameServer.StartResult? result1 = null;
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
        GameServer.StartResult? result2 = null;
        Assert.DoesNotThrow(() => result2 = gameServer.Start("p3", null));
        Assert.That(result2, Is.Not.Null);
    }


    [Test]
    public void OldGamesAreCleanedUpAfterAWhile()
    {
        var gameServer = new GameServer(database, 1);
        GivenUserRegistered(id: "u1", name: "User1");
        GivenUserRegistered(id: "u2", name: "User2");

        // Start quest for user 1 and let it timeout
        GameServer.StartResult? result1 = null;
        Assert.DoesNotThrow(() => result1 = gameServer.Start("u1", null));
        Assert.That(result1, Is.Not.Null);
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => gameServer.Act(result1.GameId, DirectedAction.MoveSouth));
        now += TimeSpan.FromSeconds(20);
        Assert.Throws<NoProgressException>(() => gameServer.Act(result1.GameId, DirectedAction.MoveNorth));
        Assert.Throws<GameFinishedException>(() => gameServer.Act(result1.GameId, DirectedAction.MoveNorth));

        // Start training for user 2 and let it timeout
        GameServer.StartResult? result2 = null;
        Assert.DoesNotThrow(() => result2 = gameServer.Start("u2", 0));
        Assert.That(result2, Is.Not.Null);
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => gameServer.Act(result2.GameId, DirectedAction.MoveEast));
        now += TimeSpan.FromSeconds(70);
        Assert.Throws<NoProgressException>(() => gameServer.Act(result2.GameId, DirectedAction.MoveWest));
        Assert.Throws<GameFinishedException>(() => gameServer.Act(result2.GameId, DirectedAction.MoveWest));

        // Wait a while
        now += TimeSpan.FromMinutes(11);

        // Start a new training
        GameServer.StartResult? result3 = null;
        Assert.DoesNotThrow(() => result3 = gameServer.Start("u1", 0));
        Assert.That(result3, Is.Not.Null);

        // Oldest two games should now be removed
        Assert.Throws<UnknownGameIdException>(() => gameServer.Act(result1.GameId, DirectedAction.MoveNorth));
        Assert.Throws<UnknownGameIdException>(() => gameServer.Act(result2.GameId, DirectedAction.MoveWest));
    }

    [Test]
    public void ThirdQuestIsQueued()
    {
        var gameServer = new GameServer(database, 2);
        GivenUserRegistered(id: "u1", name: "User1");
        GivenUserRegistered(id: "u2", name: "User2");
        GivenUserRegistered(id: "u3", name: "User2");

        // Start quest for user 1
        GameServer.StartResult? result1 = null;
        Assert.DoesNotThrow(() => result1 = gameServer.Start("u1", null));
        Assert.That(result1, Is.Not.Null);

        // Start quest for user 2
        now += TimeSpan.FromSeconds(1);
        GameServer.StartResult? result2 = null;
        Assert.DoesNotThrow(() => result2 = gameServer.Start("u2", null));
        Assert.That(result2, Is.Not.Null);

        // Start quest for user 3, should be queued
        now += TimeSpan.FromSeconds(1);
        GameServer.StartResult? result3 = null;
        Assert.Throws<QuestQueuedException>(() => result3 = gameServer.Start("u3", null));
        Assert.That(result3, Is.Null);

        // Act on user 1 quest should be possible
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => gameServer.Act(result1.GameId, DirectedAction.MoveSouth));

        // Act on user 2 quest should be possible
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => gameServer.Act(result2.GameId, DirectedAction.MoveEast));
    }

    #region Primitives

    private void GivenUserRegistered(string id = "u1", string name = "User1", int level = 1)
    {
        database.CreateUserAsync(new User { Id = id, Name = name, Level = level }).Wait();
    }

    #endregion
}