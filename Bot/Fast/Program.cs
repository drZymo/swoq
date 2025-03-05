using Swoq.Interface;
using System.Diagnostics;

namespace Bot;

public static class Program
{
    private const int MaxLevel = 7;

    private static bool print = true;
    private static bool saveReplay = true;
    private static bool train = false;
    private static bool once = false;

    public static void Main(string[] args)
    {
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
        using var connection = new GameConnection(saveReplay);

        do
        {
            try
            {
                int? level = train ? Random.Shared.Next(0, MaxLevel + 1) : null;
                PlayGame(connection, level);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex}");
            }
        } while (!once);
    }

    private static void PlayGame(GameConnection connection, int? level)
    {
        var sw = Stopwatch.StartNew();

        var game = connection.Start(level);

        var bot = new ActionPlanner(game.MapHeight, game.MapWidth, game.VisibilityRange);

        while (game.State.Status == GameStatus.Active)
        {
            // Find next action based on current state
            var action = bot.GetNextAction(game.State);

            // Print current state
            if (print)
            {
                Console.SetCursorPosition(0, 0);
                bot.PrintMap();
            }

            // Act and get new state
            game.Act(action);

            if (action == DirectedAction.None)
            {
                Console.WriteLine("Stuck");
                break;
            }
        }
        Console.WriteLine($"Finished: {game.State.Status}");

        sw.Stop();
        var ticksPerSecond = game.State.Tick / sw.Elapsed.TotalSeconds;

        Console.WriteLine($"{ticksPerSecond:F1} ticks/s");

    }
}
