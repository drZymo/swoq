using Swoq.Data;
using Swoq.Infra;
using Swoq.Interface;

namespace Swoq.Server;

public class Quest<MG> : IGame where MG : IMapGenerator
{
    private readonly User user;
    private readonly ISwoqDatabase database;

    private readonly Lock questMutex = new();

    private readonly DateTime startTime = Clock.Now;
    private int ticks = 0;
    private Game currentGame;

    public Quest(User user, ISwoqDatabase database)
    {
        this.user = user;
        this.database = database;

        currentGame = new Game(MG.Generate(Level, Parameters.MapHeight, Parameters.MapWidth), Parameters.MaxQuestInactivityTime);
        State = currentGame.State with { Tick = ticks };
    }

    public Guid Id { get; } = Guid.NewGuid();
    public int Level { get; private set; } = 0;
    public GameState State { get; private set; }
    public DateTime LastActionTime => currentGame.LastActionTime;

    public void Act(DirectedAction? action1 = null, DirectedAction? action2 = null)
    {
        lock (questMutex)
        {
            if (State.IsFinished) throw new GameFinishedException(State);

            try
            {
                // Play current game
                currentGame.Act(action1, action2);

                ticks++;

                // If game is completed successfully, then move to next level.
                if (currentGame.State.Status == GameStatus.FinishedSuccess)
                {
                    Level++;

                    // Update user stats
                    var lengthSeconds = (int)Math.Round((LastActionTime - startTime).TotalSeconds, MidpointRounding.AwayFromZero);
                    if (user.Level <= Level)
                    {
                        if (user.Level < Level)
                        {
                            // If this level was not reached before,
                            // unlock the level and remember the length.
                            user.Level = Level;
                            user.QuestLengthTicks = ticks;
                            user.QuestLengthSeconds = lengthSeconds;
                        }
                        else if (user.Level == Level)
                        {
                            // If this level was the highest level reached before, then update the duration if it improved.
                            user.QuestLengthTicks = Math.Min(ticks, user.QuestLengthTicks);
                            user.QuestLengthSeconds = Math.Min(lengthSeconds, user.QuestLengthSeconds);
                        }

                        // Sync database
                        database.UpdateUserAsync(user);
                    }

                    // Create a new game if this was not the last level
                    if (Level <= MG.MaxLevel)
                    {
                        currentGame = new Game(MG.Generate(Level, Parameters.MapHeight, Parameters.MapWidth), Parameters.MaxQuestInactivityTime);
                    }
                }
            }
            finally
            {
                State = currentGame.State with { Tick = ticks };
            }
        }
    }

    public void CheckGameIsFinished()
    {
        // A mutex is needed because this function is called every time a new game is started, which can be any thread.
        lock (questMutex)
        {
            currentGame.CheckGameIsFinished();
            State = currentGame.State with { Tick = ticks };
        }
    }
}
