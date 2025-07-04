using Microsoft.Extensions.Configuration;
using Swoq.Data;
using Swoq.Infra;
using Swoq.Interface;
using Swoq.Server;

namespace Swoq.Test;

public class FinalGameServerTests
{
    private readonly DateTime now = DateTime.Now;

    private DummyGenerator mapGenerator;
    private SwoqDatabaseInMemory database;
    private IConfiguration configuration;
    private FinalGameServer gameServer;

    [SetUp]
    public void Setup()
    {
        Clock.Setup(() => now);

        mapGenerator = new();
        database = new();
        configuration = new ConfigurationBuilder()
            .AddCommandLine(["--final", "u1,u2", "--countdown", "no"])
            .Build();
        database.CreateUser(new User { Id = "u1", Name = "User1", Level = 1 });
        database.CreateUser(new User { Id = "u2", Name = "User2", Level = 1 });
        database.CreateUser(new User { Id = "u3", Name = "User3", Level = 1 });
        gameServer = new FinalGameServer(mapGenerator, database, configuration, finalSeed: 42);
    }

    [TearDown]
    public void TearDown()
    {
        gameServer.Dispose();
    }

    [Test]
    public void UnknownUser()
    {
        Assert.That(
            Assert.Throws<GameServerStartException>(() => gameServer.Start("u4", "User4", null)).Result,
            Is.EqualTo(StartResult.UnknownUser));
    }

    [Test]
    public void TrainingNotAllowed()
    {
        Assert.That(
            Assert.Throws<GameServerStartException>(() => gameServer.Start("u1", "User1", 0)).Result,
            Is.EqualTo(StartResult.NotAllowed));
    }

    [Test]
    public void UserNotAllowed()
    {
        Assert.That(
            Assert.Throws<GameServerStartException>(() => gameServer.Start("u3", "User3", null)).Result,
            Is.EqualTo(StartResult.NotAllowed));
    }

    [Test]
    public void QuestStartsAfterAllConnected()
    {
        using var task1 = Task.Run(() => gameServer.Start("u1", "User1", null));
        Thread.Sleep(100);
        using var task2 = Task.Run(() => gameServer.Start("u2", "User2", null));

        Assert.That(task1.Wait(100), Is.True);
        Assert.That(task2.Wait(100), Is.True);

        // All games should have the same seed
        Assert.That(task1.Result.Seed, Is.EqualTo(task2.Result.Seed));
    }

    [Test]
    public void SecondQuestStartNotAllowed()
    {
        using var task1 = Task.Run(() => gameServer.Start("u1", "User1", null));
        using var task2 = Task.Run(() => gameServer.Start("u2", "User2", null));

        Assert.That(task1.Wait(100), Is.True);
        Assert.That(task2.Wait(100), Is.True);

        // Another start is not allowed
        Assert.That(
            Assert.Throws<GameServerStartException>(() => gameServer.Start("u1", "User1", null)).Result,
            Is.EqualTo(StartResult.NotAllowed));
    }

    private class DummyGenerator : IMapGenerator
    {
        public Map Generate(int level, int height, int width, Random random)
        {
            width = 5;
            height = 5;
            MutableMap map = new(level, 5, 5);

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
