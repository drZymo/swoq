using Swoq.Interface;
using System.Diagnostics;
using System.Text;

namespace Bot;

public static class Program
{
    private const int MinLevel = 21;
    private const int MaxLevel = 21;

    private static bool print = true;
    private static bool saveReplay = true;
    private static bool train = false;
    private static bool once = false;

    private static Dictionary<int, (int total, int success)> successRates = [];

    public static void Main(string[] args)
    {
        DotEnv.Load();

        foreach (var arg in args)
        {
            if (arg == "--no-print") print = false;
            if (arg == "--no-replay") saveReplay = false;
            if (arg == "--train") train = true;
            if (arg == "--once") once = true;
        }

        if (print)
        {
            Console.WriteLine("Press any key to start...");
            Console.ReadKey();
            Console.CursorVisible = false;
        }

        Play();
    }

    private static void Play()
    {
        using var connection = new GameConnection(
            Environment.GetEnvironmentVariable("SWOQ_USER_ID") ?? throw new ArgumentNullException(),
            Environment.GetEnvironmentVariable("SWOQ_USER_NAME") ?? throw new ArgumentNullException(),
            Environment.GetEnvironmentVariable("SWOQ_HOST") ?? throw new ArgumentNullException(),
            saveReplay);

        do
        {
            try
            {
                int? level = train ? Random.Shared.Next(MinLevel, MaxLevel + 1) : null;
                int? seed = DotEnv.GetEnvironmentVariableInt("SWOQ_SEED");
                PlayGame(connection, level, seed);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex}");
            }
        } while (!once);
    }

    private static void PlayGame(GameConnection connection, int? level, int? seed)
    {
        var sw = Stopwatch.StartNew();

        using var game = connection.Start(level, seed);

        try
        {
            var bot = new ActionPlanner(game.MapHeight, game.MapWidth, game.VisibilityRange);

            while (game.State.Status == GameStatus.Active)
            {
                // Find next action based on current state
                var (action1, action2) = bot.GetNextAction(game.State);

                // Print current state
                if (print)
                {
                    Console.SetCursorPosition(0, 0);
                    bot.PrintMap();
                }

                // Act and get new state
                game.Act(action1, action2);
            }
        }
        finally
        {
            sw.Stop();
            var ticksPerSecond = game.State.Tick / sw.Elapsed.TotalSeconds;

            var seedStr = game.Seed.HasValue ? $", seed {game.Seed.Value}" : string.Empty;
            Console.WriteLine($"Finished: status {game.State.Status}{seedStr}, {ticksPerSecond:F1} ticks/s");

            if (level.HasValue)
            {
                if (!successRates.TryGetValue(level.Value, out var rates))
                {
                    rates = (0, 0);
                }
                if (game.State.Status == GameStatus.FinishedSuccess)
                {
                    rates.success++;
                }
                rates.total++;
                successRates[level.Value] = rates;
            }

            var line = string.Join(", ", successRates.OrderBy(kvp => kvp.Key).Select(kvp => $"#{kvp.Key}: {kvp.Value.success/(double)kvp.Value.total:P1}"));
            Console.WriteLine(line);
        }
    }
}
