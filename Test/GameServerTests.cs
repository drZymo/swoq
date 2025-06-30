using Swoq.Data;
using Swoq.Infra;
using Swoq.Interface;
using Swoq.Server;
using System.Collections.Immutable;

namespace Swoq.Test;

public class GameServerTests
{
    private DateTime now = DateTime.Now;

    private DummyGenerator mapGenerator;
    private SwoqDatabaseInMemory database;
    private GameServer gameServer;

    [SetUp]
    public void Setup()
    {
        Clock.Setup(() => now);

        mapGenerator = new();
        database = new();
        database.CreateUser(new User { Id = "u1", Name = "User1", Level = 1 });
        database.CreateUser(new User { Id = "u2", Name = "User2", Level = 1 });
        database.CreateUser(new User { Id = "u3", Name = "User3", Level = 1 });
        gameServer = new GameServer(mapGenerator, database, 1, 2, TimeSpan.FromMilliseconds(10));
    }

    [TearDown]
    public void TearDown()
    {
        gameServer.Dispose();
    }

    [Test]
    public void UnknownUser()
    {
        Assert.That(Assert.Throws<GameServerStartException>(() => gameServer.Start("u4", 0)).Result, Is.EqualTo(StartResult.UnknownUser));
    }

    [Test]
    public void UserLevelTooLow()
    {
        Assert.That(Assert.Throws<GameServerStartException>(() => gameServer.Start("u1", 2)).Result, Is.EqualTo(StartResult.InvalidLevel));
    }

    [Test]
    public void SingleQuestCanStartAndAct()
    {
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
        List<IImmutableList<string>> queueUpdates = [];
        gameServer.QueueUpdated += (s, e) => { queueUpdates.Add(e.QueuedUsers); };

        // Start quest for user 1
        GameStartResult? result1 = null;
        Assert.DoesNotThrow(() => result1 = gameServer.Start("u1", null));
        Assert.That(result1, Is.Not.Null);

        // Check user has been in the queue
        Assert.Multiple(() =>
        {
            Assert.That(queueUpdates.Count, Is.EqualTo(2));
            Assert.That(queueUpdates[0], Is.EqualTo(["User1"]));
            Assert.That(queueUpdates[1], Is.Empty);
        });
        queueUpdates = [];

        // Start quest for user 2, it should be queued
        now += TimeSpan.FromSeconds(1);
        Assert.That(Assert.Throws<GameServerStartException>(() => gameServer.Start("u2", null)).Result, Is.EqualTo(StartResult.QuestQueued));

        // Should be in the queue
        Assert.Multiple(() =>
        {
            Assert.That(queueUpdates.Count, Is.EqualTo(1));
            Assert.That(queueUpdates[0], Is.EqualTo(["User2"]));
        });
        queueUpdates = [];

        // Act on user 1 quest should be possible
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => gameServer.Act(result1.GameId, DirectedAction.MoveSouth));

        // No more queue changes
        Assert.That(queueUpdates, Is.Empty);
    }

    [Test]
    public void TwoQuestsCanBeActive()
    {
        // Create new server with 2 quests active
        gameServer.Dispose();
        gameServer = new GameServer(mapGenerator, database, 2, 2, TimeSpan.FromMilliseconds(10));

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
        // Start quest for user 1 and 2
        GameStartResult? result1 = null;
        Assert.DoesNotThrow(() => result1 = gameServer.Start("u1", null));
        Assert.That(result1, Is.Not.Null);
        now += TimeSpan.FromSeconds(1);
        Assert.That(Assert.Throws<GameServerStartException>(() => gameServer.Start("u2", null)).Result, Is.EqualTo(StartResult.QuestQueued));

        // Let both users keep responding for a while
        now += TimeSpan.FromSeconds(4);
        Assert.DoesNotThrow(() => gameServer.Act(result1.GameId, DirectedAction.MoveSouth));
        Assert.That(Assert.Throws<GameServerStartException>(() => gameServer.Start("u2", null)).Result, Is.EqualTo(StartResult.QuestQueued));
        now += TimeSpan.FromSeconds(4);
        Assert.DoesNotThrow(() => gameServer.Act(result1.GameId, DirectedAction.MoveNorth));
        Assert.That(Assert.Throws<GameServerStartException>(() => gameServer.Start("u2", null)).Result, Is.EqualTo(StartResult.QuestQueued));

        // Stop responding with user 1 so it times out but keep responding with user 2
        now += TimeSpan.FromSeconds(4);
        Assert.That(Assert.Throws<GameServerStartException>(() => gameServer.Start("u2", null)).Result, Is.EqualTo(StartResult.QuestQueued));
        now += TimeSpan.FromSeconds(4);
        Assert.That(Assert.Throws<GameServerStartException>(() => gameServer.Start("u2", null)).Result, Is.EqualTo(StartResult.QuestQueued));

        // User 1 quest is inactive and was stopped
        // User 2 is now first in line and can start the quest
        now += TimeSpan.FromSeconds(4);
        GameStartResult? result2 = null;
        Assert.DoesNotThrow(() => result2 = gameServer.Start("u2", null));
        Assert.That(result2, Is.Not.Null);

        // Acting on 1 should now fail on timeout
        Assert.That(Assert.Throws<GameServerActException>(() => gameServer.Act(result1.GameId, DirectedAction.MoveSouth)).Result, Is.EqualTo(ActResult.GameFinished));
    }

    [Test]
    public void UserIsRemovedFromQuestQueueWhenItStopsCallingStart()
    {
        List<IImmutableList<string>> queueUpdates = [];
        gameServer.QueueUpdated += (s, e) => { queueUpdates.Add(e.QueuedUsers); };

        // Start quest for user 1, 2 and 3
        GameStartResult? result1 = null;
        Assert.DoesNotThrow(() => result1 = gameServer.Start("u1", null));
        Assert.That(result1, Is.Not.Null);
        now += TimeSpan.FromSeconds(1);
        Assert.That(Assert.Throws<GameServerStartException>(() => gameServer.Start("u2", null)).Result, Is.EqualTo(StartResult.QuestQueued));
        now += TimeSpan.FromSeconds(1);
        Assert.That(Assert.Throws<GameServerStartException>(() => gameServer.Start("u3", null)).Result, Is.EqualTo(StartResult.QuestQueued));

        // Check queue has changed
        Assert.Multiple(() =>
        {
            Assert.That(queueUpdates.Count, Is.EqualTo(4));
            Assert.That(queueUpdates[0], Is.EqualTo(["User1"]));
            Assert.That(queueUpdates[1], Is.Empty);
            Assert.That(queueUpdates[2], Is.EqualTo(["User2"]));
            Assert.That(queueUpdates[3], Is.EqualTo(["User2", "User3"]));
        });
        queueUpdates = [];

        // Let user 1 and 3 keep responding for a while
        now += TimeSpan.FromSeconds(4);
        Assert.DoesNotThrow(() => gameServer.Act(result1.GameId, DirectedAction.MoveSouth));
        Assert.That(Assert.Throws<GameServerStartException>(() => gameServer.Start("u3", null)).Result, Is.EqualTo(StartResult.QuestQueued));
        now += TimeSpan.FromSeconds(4);
        Assert.DoesNotThrow(() => gameServer.Act(result1.GameId, DirectedAction.MoveNorth));
        Assert.That(Assert.Throws<GameServerStartException>(() => gameServer.Start("u3", null)).Result, Is.EqualTo(StartResult.QuestQueued));
        now += TimeSpan.FromSeconds(4);
        Assert.DoesNotThrow(() => gameServer.Act(result1.GameId, DirectedAction.MoveSouth));
        Assert.That(Assert.Throws<GameServerStartException>(() => gameServer.Start("u3", null)).Result, Is.EqualTo(StartResult.QuestQueued));

        // User 2 should be removed from the queue
        Assert.Multiple(() =>
        {
            Assert.That(queueUpdates.Count, Is.EqualTo(1));
            Assert.That(queueUpdates[0], Is.EqualTo(["User3"]));
        });
        queueUpdates = [];

        // Stop responding with user 1 so it times out but keep responding with user 3
        now += TimeSpan.FromSeconds(4);
        Assert.That(Assert.Throws<GameServerStartException>(() => gameServer.Start("u3", null)).Result, Is.EqualTo(StartResult.QuestQueued));
        now += TimeSpan.FromSeconds(4);
        Assert.That(Assert.Throws<GameServerStartException>(() => gameServer.Start("u3", null)).Result, Is.EqualTo(StartResult.QuestQueued));

        // User 1 quest is inactive and was stopped
        // User 2 has been removed from the queue
        // User 3 is now first in line and can start the quest
        now += TimeSpan.FromSeconds(4);
        GameStartResult? result2 = null;
        Assert.DoesNotThrow(() => result2 = gameServer.Start("u3", null));
        Assert.That(result2, Is.Not.Null);

        // Queue should be empty
        Assert.Multiple(() =>
        {
            Assert.That(queueUpdates.Count, Is.EqualTo(1));
            Assert.That(queueUpdates[0], Is.Empty);
        });
    }

    [Test]
    public void OldGamesAreCleanedUpAfterAWhile()
    {
        HashSet<Guid> removedGames = [];
        gameServer.GameRemoved += (s, e) => removedGames.Add(e.GameId);

        // Start quest for user 1 and let it timeout
        GameStartResult? result1 = null;
        Assert.DoesNotThrow(() => result1 = gameServer.Start("u1", null));
        Assert.That(result1, Is.Not.Null);
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => gameServer.Act(result1.GameId, DirectedAction.MoveSouth));
        now += TimeSpan.FromSeconds(20);
        Assert.That(Assert.Throws<GameServerActException>(() => gameServer.Act(result1.GameId, DirectedAction.MoveNorth)).Result, Is.EqualTo(ActResult.GameFinished));

        // Start training for user 2 and let it timeout
        GameStartResult? result2 = null;
        Assert.DoesNotThrow(() => result2 = gameServer.Start("u2", 0));
        Assert.That(result2, Is.Not.Null);
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => gameServer.Act(result2.GameId, DirectedAction.MoveEast));
        now += TimeSpan.FromSeconds(70);
        Assert.That(Assert.Throws<GameServerActException>(() => gameServer.Act(result2.GameId, DirectedAction.MoveWest)).Result, Is.EqualTo(ActResult.GameFinished));

        // Wait a while
        now += TimeSpan.FromMinutes(11);

        // Start a new training
        GameStartResult? result3 = null;
        Assert.DoesNotThrow(() => result3 = gameServer.Start("u1", 0));
        Assert.That(result3, Is.Not.Null);

        // Oldest two games should now be removed
        Assert.That(Assert.Throws<GameServerActException>(() => gameServer.Act(result1.GameId, DirectedAction.MoveNorth)).Result, Is.EqualTo(ActResult.UnknownGameId));
        Assert.That(Assert.Throws<GameServerActException>(() => gameServer.Act(result2.GameId, DirectedAction.MoveWest)).Result, Is.EqualTo(ActResult.UnknownGameId));

        Assert.That(removedGames, Has.Member(result1.GameId));
        Assert.That(removedGames, Has.Member(result2.GameId));
    }

    [Test]
    public void ThirdQuestIsQueued()
    {
        // Create new server with 2 quests active
        gameServer.Dispose();
        gameServer = new GameServer(mapGenerator, database, 2, 2, TimeSpan.FromMilliseconds(10));

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
        Assert.That(Assert.Throws<GameServerStartException>(() => gameServer.Start("u3", null)).Result, Is.EqualTo(StartResult.QuestQueued));

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
        // Create new server with 2 quests active
        gameServer.Dispose();
        gameServer = new GameServer(mapGenerator, database, 2, 2, TimeSpan.FromMilliseconds(10));

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
        Assert.That(Assert.Throws<GameServerStartException>(() => gameServer.Start("u3", null)).Result, Is.EqualTo(StartResult.QuestQueued));

        // Act on user 1 quest should be possible
        GameState state = result1.State;
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => state = gameServer.Act(result1.GameId, DirectedAction.MoveSouth));

        // Act on user 2 quest should be possible
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => gameServer.Act(result2.GameId, DirectedAction.MoveEast));

        // 3 is still queued
        Assert.That(Assert.Throws<GameServerStartException>(() => gameServer.Start("u3", null)).Result, Is.EqualTo(StartResult.QuestQueued));

        // Finish the game with 1 and keep moving back-and-forth with 2
        Assert.That(state.Level, Is.EqualTo(0));
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => state = gameServer.Act(result1.GameId, DirectedAction.MoveEast));
        Assert.DoesNotThrow(() => gameServer.Act(result2.GameId, DirectedAction.MoveWest));
        Assert.That(Assert.Throws<GameServerStartException>(() => gameServer.Start("u3", null)).Result, Is.EqualTo(StartResult.QuestQueued));
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => state = gameServer.Act(result1.GameId, DirectedAction.MoveSouth));
        Assert.DoesNotThrow(() => gameServer.Act(result2.GameId, DirectedAction.MoveEast));
        Assert.That(Assert.Throws<GameServerStartException>(() => gameServer.Start("u3", null)).Result, Is.EqualTo(StartResult.QuestQueued));
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => state = gameServer.Act(result1.GameId, DirectedAction.MoveEast));
        Assert.DoesNotThrow(() => gameServer.Act(result2.GameId, DirectedAction.MoveWest));
        Assert.That(Assert.Throws<GameServerStartException>(() => gameServer.Start("u3", null)).Result, Is.EqualTo(StartResult.QuestQueued));
        Assert.That(state.Level, Is.EqualTo(1));
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => state = gameServer.Act(result1.GameId, DirectedAction.MoveSouth));
        Assert.DoesNotThrow(() => gameServer.Act(result2.GameId, DirectedAction.MoveEast));
        Assert.That(Assert.Throws<GameServerStartException>(() => gameServer.Start("u3", null)).Result, Is.EqualTo(StartResult.QuestQueued));
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => state = gameServer.Act(result1.GameId, DirectedAction.MoveEast));
        Assert.DoesNotThrow(() => gameServer.Act(result2.GameId, DirectedAction.MoveWest));
        Assert.That(Assert.Throws<GameServerStartException>(() => gameServer.Start("u3", null)).Result, Is.EqualTo(StartResult.QuestQueued));
        now += TimeSpan.FromSeconds(1);
        Assert.DoesNotThrow(() => state = gameServer.Act(result1.GameId, DirectedAction.MoveSouth));
        Assert.DoesNotThrow(() => gameServer.Act(result2.GameId, DirectedAction.MoveEast));
        Assert.That(Assert.Throws<GameServerStartException>(() => gameServer.Start("u3", null)).Result, Is.EqualTo(StartResult.QuestQueued));

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
        GameStartResult? result3 = null;
        Assert.DoesNotThrow(() => result3 = gameServer.Start("u3", null));
        // User 1 cannot act anymore
        Assert.That(Assert.Throws<GameServerActException>(() => state = gameServer.Act(result1.GameId, DirectedAction.MoveEast)).Result, Is.EqualTo(ActResult.GameFinished));
        // User 2 can still act
        Assert.DoesNotThrow(() => gameServer.Act(result2.GameId, DirectedAction.MoveEast));
    }

    [Test]
    public void ActiveUserQuestStoppedOnSecondStart()
    {
        // Start quest for user 1
        GameStartResult? result1 = null;
        Assert.DoesNotThrow(() => result1 = gameServer.Start("u1", null));
        Assert.That(result1, Is.Not.Null);

        // Start quest for user 2, should be queued
        now += TimeSpan.FromSeconds(1);
        Assert.That(Assert.Throws<GameServerStartException>(() => gameServer.Start("u2", null)).Result, Is.EqualTo(StartResult.QuestQueued));

        // Start another quest for user 1, should not be queued
        now += TimeSpan.FromSeconds(1);
        Assert.That(Assert.Throws<GameServerStartException>(() => gameServer.Start("u1", null)).Result, Is.EqualTo(StartResult.QuestQueued));

        // Act on user 1's initial quest should no longer be possible
        now += TimeSpan.FromSeconds(1);
        Assert.That(Assert.Throws<GameServerActException>(() => gameServer.Act(result1.GameId, DirectedAction.MoveSouth)).Result, Is.EqualTo(ActResult.GameFinished));
    }

    private class DummyGenerator : IMapGenerator
    {
        public Map Generate(int level, int height, int width, Random random)
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

        public int MaxLevel { get; } = 1;
    }
}
