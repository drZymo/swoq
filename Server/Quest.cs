using Swoq.Data;
using Swoq.Infra;
using Swoq.Interface;

namespace Swoq.Server;

public class Quest : IGame
{
    private readonly User user;
    private readonly IMapGenerator mapGenerator;
    private readonly ISwoqDatabase database;
    private readonly int seed;
    private readonly UserStatisticsReporter reporter;

    private readonly DateTime startTime = Clock.Now;
    private int ticks = 0;
    private int level = 0;
    private Game currentGame;

    public Quest(User user, IMapGenerator mapGenerator, ISwoqDatabase database, int seed)
    {
        this.user = user;
        this.mapGenerator = mapGenerator;
        this.database = database;
        this.seed = seed;
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
                        user.QuestFinished = user.Level > mapGenerator.MaxLevel;
                        user.BestQuestId = Id.ToString();
                    }
                    else if (user.Level == level)
                    {
                        // If this level was the highest level reached before, then update the duration if it improved.
                        if (ticks <= user.QuestLengthTicks)
                        {
                            if (ticks < user.QuestLengthTicks)
                            {
                                user.QuestLengthTicks = ticks;
                                user.QuestLengthSeconds = lengthSeconds;
                            }
                            else if (lengthSeconds < user.QuestLengthSeconds)
                            {
                                user.QuestLengthSeconds = lengthSeconds;
                            }
                            user.BestQuestId = Id.ToString();
                        }
                    }

                    // Sync database
                    database.UpdateUserAsync(user);
                }

                // Create a new game if this was not the last level
                if (level <= mapGenerator.MaxLevel)
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

    public void Cancel()
    {
        currentGame.Cancel();
        State = currentGame.State with { Tick = ticks, Status = GameStatus.FinishedCancelled };
    }

    private Game NewGame()
    {
        // Use the same seed for every level in this quest
        var random = new Random(seed + level);

        var map = mapGenerator.Generate(level, Parameters.MapHeight, Parameters.MapWidth, random);
        return new Game(map,
                        Parameters.MaxQuestInactivityTime,
                        Parameters.MaxLevelTicks,
                        Parameters.MaxLevelDuration,
                        random,
                        reporter);
    }
}
