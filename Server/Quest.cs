using Swoq.Data;
using Swoq.Infra;
using Swoq.Interface;

namespace Swoq.Server;

public class Quest<MG> : IGame where MG : IMapGenerator
{
    private readonly User user;
    private readonly ISwoqDatabase database;
    private readonly Random random;
    private readonly UserStatisticsReporter reporter;

    private readonly DateTime startTime = Clock.Now;
    private int ticks = 0;
    private int level = 0;
    private Game currentGame;

    public Quest(User user, ISwoqDatabase database)
    {
        this.user = user;
        this.database = database;
        this.random = new();
        this.reporter = new(user, database);

        currentGame = NewGame();
        State = currentGame.State with { Tick = ticks };
    }

    public Guid Id { get; } = Guid.NewGuid();
    public GameState State { get; private set; }
    public DateTime LastActionTime => currentGame.LastActionTime;
    public bool IsFinished => State.Status != GameStatus.Active || TimedOut;

    private bool TimedOut => (Clock.Now - LastActionTime) > Parameters.MaxQuestInactivityTime;

    public void Act(DirectedAction? action1 = null, DirectedAction? action2 = null)
    {
        if (State.Status == GameStatus.Active && TimedOut)
        {
            State = State with { Status = GameStatus.FinishedTimeout };
        }
        if (IsFinished) throw new GameFinishedException();

        try
        {
            // Play current game
            currentGame.Act(action1, action2);

            ticks++;

            // If game is completed successfully, then move to next level.
            if (currentGame.State.Status == GameStatus.FinishedSuccess)
            {
                level++;

                // Update user stats
                var lengthSeconds = (int)Math.Round((LastActionTime - startTime).TotalSeconds, MidpointRounding.AwayFromZero);
                if (user.Level <= level)
                {
                    if (user.Level < level)
                    {
                        // If this level was not reached before,
                        // unlock the level and remember the length.
                        user.Level = level;
                        user.QuestLengthTicks = ticks;
                        user.QuestLengthSeconds = lengthSeconds;
                    }
                    else if (user.Level == level)
                    {
                        // If this level was the highest level reached before, then update the duration if it improved.
                        user.QuestLengthTicks = Math.Min(ticks, user.QuestLengthTicks);
                        user.QuestLengthSeconds = Math.Min(lengthSeconds, user.QuestLengthSeconds);
                    }

                    // Sync database
                    database.UpdateUserAsync(user);
                }

                // Create a new game if this was not the last level
                if (level <= MG.MaxLevel)
                {
                    currentGame = NewGame();
                }
            }
        }
        finally
        {
            State = currentGame.State with { Tick = ticks };
        }
    }

    private Game NewGame()
    {
        var map = MG.Generate(level, Parameters.MapHeight, Parameters.MapWidth, random);
        return new Game(map, Parameters.MaxQuestInactivityTime, random, reporter);
    }
}
