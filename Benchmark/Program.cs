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
    private readonly MapGenerator mapGenerator = new();

    [Benchmark]
    public void GenerateAll()
    {
        var random = new Random(42);
        for (var l = 0; l <= mapGenerator.MaxLevel; l++)
        {
            mapGenerator.Generate(l, 64, 64, random);
        }
    }

    [Benchmark]
    public void GenerateLevel1()
    {
        var random = new Random(42);
        mapGenerator.Generate(1, 64, 64, random);
    }

    [Benchmark]
    public void GenerateLevel4()
    {
        var random = new Random(42);
        mapGenerator.Generate(4, 64, 64, random);
    }
}

[InProcess]
public class GameServerBenchmark : IDisposable
{
    private static readonly MapGenerator mapGenerator = new();
    private readonly SwoqDatabaseInMemory database = new();
    private readonly GameServer server;

    public GameServerBenchmark()
    {
        for (var i = 0; i < 10; i++)
        {
            database.CreateUser(new User() { Id = $"u{i}", Name = $"User_{i}", Level = 23 });
        }
        server = new GameServer(mapGenerator, database);
    }

    [GlobalCleanup]
    public void Dispose()
    {
        server.Dispose();
    }

    [Benchmark]
    public void SingleGame()
    {
        (int a1, int a2)[] actions = [(3, 3), (2, 2), (3, 2), (3, 1), (2, 0), (2, 0), (2, 0), (2, 0), (2, 3), (2, 2), (2, 2), (2, 2), (3, 3), (0, 3), (0, 3), (0, 3), (0, 3), (0, 4), (0, 4), (0, 3), (0, 3), (2, 3), (2, 3), (2, 3), (2, 3), (2, 3), (2, 0), (2, 3), (2, 3), (2, 3), (2, 3), (2, 3), (3, 3), (3, 3), (3, 0), (2, 3), (3, 3), (1, 3), (1, 3), (1, 3), (2, 3), (2, 4), (2, 3), (2, 3), (2, 3), (1, 2), (2, 3), (1, 3), (3, 3), (3, 3), (2, 3), (2, 3), (2, 3), (2, 3), (2, 0), (2, 3), (2, 0), (2, 1), (2, 2), (2, 2), (2, 2), (3, 1), (2, 2), (2, 1), (2, 1), (2, 1), (2, 2), (2, 3), (2, 0), (2, 3), (3, 3), (3, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (1, 2), (2, 2), (2, 2), (2, 2), (1, 0), (1, 3), (3, 2), (3, 2), (3, 2), (3, 2), (3, 2), (3, 3), (3, 2), (2, 2), (3, 2), (3, 2), (3, 2), (3, 1), (3, 2), (3, 2), (3, 1), (6, 1), (6, 1), (6, 1), (6, 3), (1, 3), (4, 3), (1, 3), (1, 3), (1, 3), (4, 2), (4, 2), (4, 2), (4, 2), (4, 2), (4, 2), (4, 2), (4, 2), (3, 2), (3, 2), (3, 2), (4, 2), (4, 3), (4, 2), (4, 2), (4, 2), (4, 2), (4, 2), (1, 0), (1, 1), (4, 2), (4, 1), (4, 1), (4, 1), (4, 1), (1, 4), (4, 4), (3, 4), (3, 1), (1, 1), (1, 1), (2, 1), (2, 1), (2, 1), (2, 1), (2, 1), (2, 4), (2, 4), (2, 1), (2, 1), (2, 2), (2, 1), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 6), (2, 6), (2, 6), (2, 6), (2, 3), (2, 3), (1, 3), (1, 3), (1, 2), (1, 1), (1, 1), (1, 1), (1, 1), (1, 4), (4, 4), (4, 4), (4, 4), (4, 4), (4, 4), (4, 4), (4, 4), (1, 4), (4, 4), (4, 4), (4, 4), (4, 4), (4, 4), (4, 4), (4, 4), (4, 4), (4, 4), (4, 4), (4, 4), (4, 4), (4, 0), (4, 4), (4, 4), (4, 3), (4, 4), (4, 4), (4, 4), (4, 4), (4, 4), (4, 4), (4, 4), (4, 4), (4, 0), (3, 4), (3, 1), (4, 4), (3, 0), (3, 4), (3, 4), (3, 4), (3, 3), (3, 3), (3, 2), (3, 2), (4, 2), (3, 2), (1, 1), (1, 2), (1, 1), (1, 8), (1, 0), (1, 8), (1, 1), (1, 3), (1, 2), (1, 2), (1, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (6, 2), (6, 2), (1, 2), (4, 2), (4, 2), (4, 2), (4, 2), (4, 2), (4, 2), (4, 2), (4, 2), (4, 2), (4, 2), (4, 2), (4, 2), (1, 2), (4, 2), (4, 6), (4, 2), (4, 2), (4, 2), (4, 2), (4, 2), (4, 2), (4, 2), (4, 2), (4, 3), (4, 3), (1, 4), (4, 4), (4, 3), (4, 3), (4, 3), (8, 3), (2, 8), (2, 8), (2, 8), (2, 1), (3, 1), (3, 1), (2, 1), (2, 2), (2, 2), (2, 2), (2, 1), (2, 1), (2, 1), (2, 1), (2, 4), (2, 4), (3, 1), (2, 0), (2, 1), (2, 1), (2, 1), (2, 1), (2, 1), (2, 1), (2, 1), (2, 1), (2, 4), (2, 1), (2, 1), (2, 0), (2, 1), (2, 0), (2, 1), (2, 1), (2, 1), (4, 1), (4, 1), (4, 3), (4, 3), (1, 3), (4, 1), (4, 1), (4, 1), (4, 4), (4, 4), (4, 4), (4, 4), (4, 4), (4, 4), (4, 4), (4, 4), (4, 0), (1, 1), (4, 4), (4, 4), (4, 4), (4, 4), (4, 4), (4, 4), (4, 4), (4, 4), (4, 4), (4, 4), (4, 4), (4, 4), (4, 4), (0, 4), (0, 1), (0, 4), (0, 4), (0, 4), (0, 4), (0, 4), (0, 4), (0, 0), (0, 4), (0, 4), (0, 4), (0, 4), (0, 4), (0, 4), (0, 4), (0, 4), (0, 4), (0, 4), (0, 4), (0, 4), (0, 4), (0, 4), (0, 4), (0, 4), (0, 4), (0, 3), (0, 3), (0, 3), (0, 3), (0, 3), (2, 3), (3, 3), (3, 3), (3, 3), (1, 1), (1, 1), (2, 1), (2, 1), (2, 0), (2, 1), (1, 1), (4, 1), (4, 1), (4, 1), (4, 2), (4, 2), (0, 2), (0, 2), (0, 2), (0, 2), (0, 2), (0, 2), (0, 2), (0, 3), (0, 2), (0, 2), (0, 2), (0, 2), (0, 2), (0, 2), (0, 2), (0, 2), (0, 2), (0, 2), (0, 2), (0, 2), (0, 2), (0, 2), (0, 2), (0, 2), (0, 2), (0, 2), (0, 3), (0, 2), (0, 2), (0, 2), (0, 2), (0, 2), (0, 2), (0, 2), (0, 2), (0, 3), (0, 2), (0, 2), (0, 0), (0, 2), (0, 2), (0, 2), (0, 3), (0, 3), (2, 3), (3, 3), (2, 2), (2, 0), (2, 2), (2, 2), (2, 2), (2, 3), (2, 3), (2, 3), (2, 3), (2, 3), (3, 3), (2, 3), (2, 3), (2, 3), (2, 3), (2, 2), (2, 3), (2, 3), (2, 1), (2, 1), (2, 1), (2, 1), (2, 1), (2, 1), (2, 1), (2, 1), (2, 0), (2, 4), (2, 4), (3, 4), (2, 4), (2, 0), (2, 4), (2, 4), (2, 4), (2, 4), (2, 4), (2, 4), (3, 4), (2, 4), (2, 1), (2, 2), (2, 2), (2, 2), (3, 2), (3, 2), (3, 2), (3, 0), (2, 2), (2, 2), (2, 0), (2, 2), (3, 2), (3, 4), (3, 0), (3, 4), (4, 4), (4, 4), (4, 8), (4, 8), (4, 8), (4, 4), (3, 2), (3, 3), (2, 4), (2, 4), (2, 4), (2, 4), (2, 4), (2, 4), (3, 4), (3, 4), (3, 4), (3, 0), (2, 4), (3, 4), (3, 4), (3, 4), (3, 4), (3, 4), (3, 4), (3, 4), (3, 4), (3, 4), (3, 4), (3, 2), (3, 2), (3, 2), (3, 2), (3, 2), (2, 2), (2, 2), (6, 2), (2, 2), (2, 2), (0, 0), (0, 2), (0, 2), (0, 3), (0, 3), (0, 2), (0, 2), (0, 2), (0, 2), (0, 2), (0, 0), (0, 2), (0, 2), (0, 2), (0, 2), (0, 2), (0, 2), (0, 2), (0, 2), (0, 3), (0, 3), (0, 3), (0, 3), (0, 2), (0, 3), (0, 3), (0, 3), (0, 3), (0, 3), (0, 3), (0, 3), (0, 3), (0, 3), (0, 3), (0, 3), (0, 3), (0, 3), (0, 3), (0, 3), (0, 2), (0, 2), (0, 2), (0, 2)];

        var startResult = server.Start("u1", "User_1", 20, 22);
        foreach (var (a1, a2) in actions)
        {
            server.Act(startResult.GameId, a1 != 0 ? (DirectedAction)a1 : null, a2 != 0 ? (DirectedAction)a2 : null);
        }
    }

    [Benchmark]
    public void StartParallel()
    {
        const int nrThreads = 10;
        using var barrier = new Barrier(nrThreads + 1);

        // Create and start all threads
        var threads = Enumerable.Range(0, nrThreads).Select(i => new Thread(() => StartThread(barrier, $"u{i}", $"User_{i}"))).ToArray();
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

    private void StartThread(Barrier barrier, string userId, string userName)
    {
        barrier.SignalAndWait();

        for (var level = 0; level < 23; level++)
        {
            server.Start(userId, userName, level);
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
                using var benchmark = new GameServerBenchmark();
                benchmark.SingleGame();
                return;
            }
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
