using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Swoq.Data;
using Swoq.Infra;
using Swoq.Interface;
using Swoq.Server;

namespace Swoq.Benchmark;

[InProcess]
public class MapGeneratorBenchmarks
{
    [Benchmark]
    public void GenerateAll()
    {
        Rnd.SetSeed(42);
        for (var l = 0; l <= MapGenerator.MaxLevel; l++)
        {
            MapGenerator.Generate(l);
        }
    }

    [Benchmark]
    public void GenerateLevel1()
    {
        Rnd.SetSeed(42);
        MapGenerator.Generate(1);
    }

    [Benchmark]
    public void GenerateLevel4()
    {
        Rnd.SetSeed(42);
        MapGenerator.Generate(4);
    }
}

[InProcess]
public class GameServerBenchmark
{
    private readonly SwoqDatabaseInMemory database;
    private readonly GameServer<MapGenerator> server;

    public GameServerBenchmark()
    {
        database = new SwoqDatabaseInMemory();
        for (var i = 0; i < 10; i++)
        {
            database.CreateUser(new User() { Id = $"u{i}", Name = $"User {i}", Level = 23 });
        }
        server = new GameServer<MapGenerator>(database);
    }

    [Benchmark]
    public void SingleGame()
    {
        (int a1, int a2)[] actions = [(3, 1), (4, 3), (3, 0), (3, 1), (2, 2), (2, 2), (1, 3), (3, 0), (2, 0), (2, 0), (2, 0), (2, 0), (2, 3), (2, 4), (2, 1), (2, 1), (1, 4), (0, 2), (0, 2), (0, 4), (0, 3), (0, 3), (0, 3), (0, 3), (0, 3), (0, 3), (0, 3), (0, 2), (0, 3), (0, 3), (3, 3), (3, 3), (4, 3), (3, 3), (3, 3), (3, 3), (3, 3), (3, 3), (3, 3), (3, 3), (3, 3), (3, 3), (3, 3), (2, 3), (2, 3), (2, 3), (2, 3), (2, 3), (1, 3), (1, 3), (1, 3), (1, 3), (1, 3), (1, 3), (1, 3), (1, 3), (1, 3), (1, 3), (2, 3), (2, 3), (2, 3), (2, 3), (2, 3), (2, 3), (3, 3), (3, 3), (3, 2), (2, 3), (3, 3), (3, 3), (3, 3), (3, 3), (3, 3), (2, 3), (2, 2), (2, 2), (3, 2), (3, 2), (3, 2), (1, 1), (1, 1), (1, 1), (1, 2), (4, 2), (4, 1), (1, 1), (1, 1), (1, 2), (1, 2), (1, 2), (1, 2), (2, 2), (2, 2), (2, 2), (2, 2), (1, 2), (1, 3), (2, 2), (2, 2), (2, 2), (2, 2), (2, 3), (2, 3), (2, 3), (2, 3), (2, 2), (3, 2), (2, 2), (2, 2), (3, 2), (3, 2), (3, 2), (3, 2), (3, 1), (3, 1), (3, 1), (3, 1), (3, 1), (3, 1), (4, 1), (4, 2), (4, 2), (4, 2), (4, 1), (4, 1), (2, 3), (2, 3), (2, 3), (2, 3), (2, 3), (2, 3), (2, 3), (1, 3), (2, 3), (2, 4), (1, 4), (1, 4), (1, 4), (2, 4), (2, 4), (2, 4), (2, 1), (2, 1), (2, 1), (2, 4), (2, 4), (2, 1), (1, 4), (1, 4), (1, 4), (1, 4), (1, 4), (1, 4), (1, 4), (1, 4), (1, 4), (1, 4), (3, 4), (3, 4), (3, 3), (2, 3), (2, 3), (3, 3), (3, 3), (3, 3), (3, 3), (3, 3), (2, 3), (2, 3), (2, 3), (2, 2), (2, 2), (2, 2), (3, 2), (3, 2), (3, 2), (3, 2), (3, 2), (3, 2), (3, 2), (3, 2), (3, 2), (3, 2), (3, 2), (4, 2), (4, 2), (4, 2), (4, 2), (4, 2), (3, 2), (2, 2), (2, 2), (3, 2), (3, 2), (2, 2), (2, 2), (2, 2), (3, 0), (3, 2), (3, 1), (6, 1), (6, 1), (6, 1), (6, 1), (3, 1), (3, 0), (3, 1), (4, 2), (4, 2), (4, 0), (4, 2), (4, 2), (4, 2), (1, 2), (1, 3), (4, 3), (4, 3), (4, 2), (4, 1), (4, 1), (1, 1), (1, 1), (1, 1), (3, 4), (3, 4), (3, 4), (3, 4), (3, 4), (4, 4), (4, 1), (4, 1), (4, 1), (4, 1), (4, 1), (4, 1), (4, 1), (4, 2), (1, 2), (1, 2), (3, 2), (3, 1), (4, 1), (4, 1), (4, 2), (4, 2), (4, 2), (4, 1), (1, 1), (1, 1), (4, 1), (4, 2), (4, 6), (4, 6), (4, 6), (4, 6), (4, 4), (4, 4), (3, 4), (3, 4), (2, 0), (2, 4), (2, 4), (2, 1), (2, 4), (2, 4), (2, 4), (2, 4), (2, 4), (2, 4), (2, 4), (2, 4), (2, 0), (2, 4), (2, 4), (6, 4), (6, 4), (3, 4), (1, 1), (2, 1), (2, 4), (3, 1), (2, 3), (2, 7), (2, 7), (2, 4), (2, 3), (2, 0), (2, 3), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (3, 2), (2, 2), (7, 2), (3, 2), (3, 2), (3, 2), (3, 2), (4, 2), (3, 2), (2, 2), (3, 1), (3, 2), (3, 2), (4, 2), (8, 2), (8, 6), (1, 2), (7, 2), (1, 3), (7, 2), (7, 2), (7, 2), (4, 4), (2, 2), (3, 1), (3, 0), (3, 0), (3, 4), (3, 4), (3, 4), (3, 4), (2, 4), (2, 3), (2, 3), (7, 4), (3, 1), (2, 1), (3, 2), (0, 2), (0, 2), (0, 3), (0, 3), (0, 2), (0, 2), (0, 2), (0, 3), (0, 3), (0, 3), (0, 3), (0, 0), (0, 3), (0, 3), (0, 3), (0, 3), (0, 2), (0, 2), (0, 2), (0, 2), (0, 3)];

        Rnd.SetSeed(42);
        var startResult = server.Start("u1", 20);
        foreach (var (a1, a2) in actions)
        {
            server.Act(startResult.GameId, a1 != 0 ? (DirectedAction)a1 : null, a2 != 0 ? (DirectedAction)a2 : null);
        }
    }

    [Benchmark]
    public void StartParallel()
    {
        const int nrThreads = 10;
        var barrier = new Barrier(nrThreads + 1);

        // Create and start all threads
        var threads = Enumerable.Range(0, nrThreads).Select(i => new Thread(() => StartThread(barrier, $"u{i}"))).ToArray();
        foreach (var thread in threads)
        {
            thread.Start();
        }

        // Wait for all threads to be started
        barrier.SignalAndWait();

        // Wait for all threads to finish
        foreach (var thread in threads)
        {
            thread.Join();
        }
    }

    private void StartThread(Barrier barrier, string userId)
    {
        barrier.SignalAndWait();

        for (var level = 0; level < 23; level++)
        {
            server.Start(userId, level);
        }
    }
}

public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length == 1)
        {
            if (args[0] == "mapall")
            {
                new MapGeneratorBenchmarks().GenerateAll();
                return;
            }
            else if (args[0] == "game")
            {
                new GameServerBenchmark().SingleGame();
                return;
            }
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
