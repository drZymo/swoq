
using Swoq.Infra;
using Swoq.Server;
using Swoq.Server.Data;
using Swoq.Server.Services;

namespace Swoq.Test;

public class GameServerTests
{
    private DateTime now = DateTime.Now;

    private SwoqDatabaseInMemory database;
    private GameServer gameServer;

    [SetUp]
    public void Setup()
    {
        Clock.Setup(() => now);

        database = new();
        gameServer = new(database);
    }

    [Test]
    public void UnknownPlayer()
    {
        Assert.Throws<UnknownPlayerException>(() => gameServer.Start("1234", 0));
    }

    [Test]
    public void LevelNotAvailable()
    {
        GivenPlayerRegistered();
        Assert.Throws<LevelNotAvailableException>(() => gameServer.Start("1234", 2));
    }

    [Test]
    public void QuestStart()
    {
        GivenPlayerRegistered();
        GameServer.StartResult? result = null;
        Assert.DoesNotThrow(() => result = gameServer.Start("1234", null));

        Assert.NotNull(result);
        Assert.That(result.PlayerName.Equals("Player1"));
    }

    [Test]
    public void QuestQueued()
    {
        GivenPlayerRegistered(id: "1234", name: "Player1");
        GivenPlayerRegistered(id: "2345", name: "Player2");
        Assert.DoesNotThrow(() => gameServer.Start("1234", null));

        now += TimeSpan.FromSeconds(1);
        Assert.Throws<QuestQueuedException>(() => gameServer.Start("2345", null));
    }

    #region Primitives

    private void GivenPlayerRegistered(string id = "1234", string name = "Player1", int level = 1)
    {
        database.CreatePlayerAsync(new Player { Id = id, Name = "Player1", Level = level }).Wait();
    }

    #endregion
}