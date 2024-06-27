using Swoq.Infra;
using Swoq.Server;
using Swoq.Server.Data;

namespace Swoq.Test;

public class GameServerTests
{
    private DateTime now = DateTime.Now;

    private SwoqDatabaseInMemory database;
    private readonly MapGenerator mapGenerator = new(64, 64);
    private GameServer gameServer;

    [SetUp]
    public void Setup()
    {
        Clock.Setup(() => now);

        database = new();
        gameServer = new(database, mapGenerator);
    }

    [Test]
    public void UnknownPlayer()
    {
        Assert.Throws<UnknownPlayerException>(() => gameServer.Start("p1", 0));
    }

    [Test]
    public void LevelNotAvailable()
    {
        GivenPlayerRegistered();
        Assert.Throws<LevelNotAvailableException>(() => gameServer.Start("p1", 2));
    }

    [Test]
    public void SingleQuestCanStartAndAct()
    {
        GivenPlayerRegistered();

        // Start a quest
        GameServer.StartResult? result = null;
        Assert.DoesNotThrow(() => result = gameServer.Start("p1", null));
        Assert.That(result, Is.Not.Null);
        Assert.That(result.PlayerName, Is.EqualTo("Player1"));

        // Act on it
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => gameServer.Act(result.GameId, new DirectedAction(Server.Action.Move, Direction.South)));
    }

    [Test]
    public void SecondQuestStartIsQueued()
    {
        GivenPlayerRegistered(id: "p1", name: "Player1");
        GivenPlayerRegistered(id: "p2", name: "Player2");

        // Start quest for player 1
        GameServer.StartResult? result1 = null;
        Assert.DoesNotThrow(() => result1 = gameServer.Start("p1", null));
        Assert.That(result1, Is.Not.Null);

        // Start quest for player 2, it should be queued
        now += TimeSpan.FromSeconds(1);
        GameServer.StartResult? result2 = null;
        Assert.Throws<QuestQueuedException>(() => result2 = gameServer.Start("p2", null));
        Assert.That(result2, Is.Null);

        // Act on player 1 quest should be possible
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => gameServer.Act(result1.GameId, new DirectedAction(Server.Action.Move, Direction.South)));
    }

    [Test]
    public void TimeoutOnQuestWillFinishItAndAllowsNextInQueueToStart()
    {
        GivenPlayerRegistered(id: "p1", name: "Player1");
        GivenPlayerRegistered(id: "p2", name: "Player2");

        // Start quest for player 1 and 2
        GameServer.StartResult? result1 = null;
        Assert.DoesNotThrow(() => result1 = gameServer.Start("p1", null));
        Assert.That(result1, Is.Not.Null);
        now += TimeSpan.FromSeconds(1);
        Assert.Throws<QuestQueuedException>(() => gameServer.Start("p2", null));

        // Let both players keep responding for a while
        now += TimeSpan.FromSeconds(4);
        Assert.DoesNotThrow(() => gameServer.Act(result1.GameId, new DirectedAction(Server.Action.Move, Direction.South)));
        Assert.Throws<QuestQueuedException>(() => gameServer.Start("p2", null));
        now += TimeSpan.FromSeconds(4);
        Assert.DoesNotThrow(() => gameServer.Act(result1.GameId, new DirectedAction(Server.Action.Move, Direction.North)));
        Assert.Throws<QuestQueuedException>(() => gameServer.Start("p2", null));

        // Stop responding with player 1 so it times out but keep responding with player 2
        now += TimeSpan.FromSeconds(4);
        Assert.Throws<QuestQueuedException>(() => gameServer.Start("p2", null));
        now += TimeSpan.FromSeconds(4);
        Assert.Throws<QuestQueuedException>(() => gameServer.Start("p2", null));

        // Player 1 quest is inactive and was stopped
        // Player 2 is now first in line and can start the quest
        now += TimeSpan.FromSeconds(4);
        GameServer.StartResult? result2 = null;
        Assert.DoesNotThrow(() => result2 = gameServer.Start("p2", null));
        Assert.That(result2, Is.Not.Null);

        // Acting on 1 should now fail on timeout
        Assert.Throws<GameTimeoutException>(() => gameServer.Act(result1.GameId, new DirectedAction(Server.Action.Move, Direction.South)));
    }

    [Test]
    public void PlayerIsRemovedFromQuestQueueWhenItStopsCallingStart()
    {
        GivenPlayerRegistered(id: "p1", name: "Player1");
        GivenPlayerRegistered(id: "p2", name: "Player2");
        GivenPlayerRegistered(id: "p3", name: "Player3");

        // Start quest for player 1, 2 and 3
        GameServer.StartResult? result1 = null;
        Assert.DoesNotThrow(() => result1 = gameServer.Start("p1", null));
        Assert.That(result1, Is.Not.Null);
        now += TimeSpan.FromSeconds(1);
        Assert.Throws<QuestQueuedException>(() => gameServer.Start("p2", null));
        now += TimeSpan.FromSeconds(1);
        Assert.Throws<QuestQueuedException>(() => gameServer.Start("p3", null));

        // Let player 1 and 3 keep responding for a while
        now += TimeSpan.FromSeconds(4);
        Assert.DoesNotThrow(() => gameServer.Act(result1.GameId, new DirectedAction(Server.Action.Move, Direction.South)));
        Assert.Throws<QuestQueuedException>(() => gameServer.Start("p3", null));
        now += TimeSpan.FromSeconds(4);
        Assert.DoesNotThrow(() => gameServer.Act(result1.GameId, new DirectedAction(Server.Action.Move, Direction.North)));
        Assert.Throws<QuestQueuedException>(() => gameServer.Start("p3", null));

        // Stop responding with player 1 so it times out but keep responding with player 3
        now += TimeSpan.FromSeconds(4);
        Assert.Throws<QuestQueuedException>(() => gameServer.Start("p3", null));
        now += TimeSpan.FromSeconds(4);
        Assert.Throws<QuestQueuedException>(() => gameServer.Start("p3", null));

        // Player 1 quest is inactive and was stopped
        // Player 2 has been removed from the queue
        // Player 3 is now first in line and can start the quest
        now += TimeSpan.FromSeconds(4);
        GameServer.StartResult? result2 = null;
        Assert.DoesNotThrow(() => result2 = gameServer.Start("p3", null));
        Assert.That(result2, Is.Not.Null);
    }


    [Test]
    public void OldGamesAreCleanedUpAfterAWhile()
    {
        GivenPlayerRegistered(id: "p1", name: "Player1");
        GivenPlayerRegistered(id: "p2", name: "Player2");

        // Start quest for player 1 and let it timeout
        GameServer.StartResult? result1 = null;
        Assert.DoesNotThrow(() => result1 = gameServer.Start("p1", null));
        Assert.That(result1, Is.Not.Null);
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => gameServer.Act(result1.GameId, new DirectedAction(Server.Action.Move, Direction.South)));
        now += TimeSpan.FromSeconds(20);
        Assert.Throws<GameTimeoutException>(() => gameServer.Act(result1.GameId, new DirectedAction(Server.Action.Move, Direction.North)));
        Assert.Throws<GameFinishedException>(() => gameServer.Act(result1.GameId, new DirectedAction(Server.Action.Move, Direction.North)));

        // Start training for player 2 and let it timeout
        GameServer.StartResult? result2 = null;
        Assert.DoesNotThrow(() => result2 = gameServer.Start("p2", 0));
        Assert.That(result2, Is.Not.Null);
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => gameServer.Act(result2.GameId, new DirectedAction(Server.Action.Move, Direction.East)));
        now += TimeSpan.FromSeconds(70);
        Assert.Throws<GameTimeoutException>(() => gameServer.Act(result2.GameId, new DirectedAction(Server.Action.Move, Direction.West)));
        Assert.Throws<GameFinishedException>(() => gameServer.Act(result2.GameId, new DirectedAction(Server.Action.Move, Direction.West)));

        // Wait a while
        now += TimeSpan.FromMinutes(11);

        // Start a new training
        GameServer.StartResult? result3 = null;
        Assert.DoesNotThrow(() => result3 = gameServer.Start("p1", 0));
        Assert.That(result3, Is.Not.Null);

        // Oldest two games should now be removed
        Assert.Throws<UnknownGameIdException>(() => gameServer.Act(result1.GameId, new DirectedAction(Server.Action.Move, Direction.North)));
        Assert.Throws<UnknownGameIdException>(() => gameServer.Act(result2.GameId, new DirectedAction(Server.Action.Move, Direction.West)));
    }


    #region Primitives

    private void GivenPlayerRegistered(string id = "p1", string name = "Player1", int level = 1)
    {
        database.CreatePlayerAsync(new Player { Id = id, Name = name, Level = level }).Wait();
    }

    #endregion
}