using Swoq.Infra;
using Swoq.Interface;
using Swoq.Server.Data;

namespace Swoq.Server;

public class Quest : IGame
{
    private readonly User user;
    private readonly ISwoqDatabase database;
    private readonly IMapGenerator mapGenerator;
    private readonly DateTime startTime;

    private Game currentGame;
    private int ticks = 0;

    public Quest(User user, ISwoqDatabase database, IMapGenerator mapGenerator)
    {
        this.user = user;
        this.database = database;
        this.mapGenerator = mapGenerator;
        this.startTime = Clock.Now;

        Id = Guid.NewGuid();
        currentGame = new Game(mapGenerator.Generate(Level), Parameters.MaxQuestInactivityTime);
        State = currentGame.State;
    }

    public Guid Id { get; }
    public DateTime LastAction { get; private set; } = Clock.Now;

    public int Level { get; private set; } = 0;
    public GameState State { get; private set; }
    public DateTime LastActionTime => currentGame.LastActionTime;

    public void Act(DirectedAction? action1 = null, DirectedAction? action2 = null)
    {
        if (State.Finished) throw new GameFinishedException(State);

        LastAction = Clock.Now;
        ticks++;

        // Play current game
        currentGame.Act(action1, action2);
        var state = currentGame.State;

        // If game is completed successfully, then move to next level.
        // If game is finished unsuccessfully, then an exception has been thrown and this line is not reached.
        if (state.Finished)
        {
            Level++;

            // Update user stats
            var lengthSeconds = (int)Math.Round((LastAction - startTime).TotalSeconds, MidpointRounding.AwayFromZero);
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
                user.QuestLengthTicks = ticks < user.QuestLengthTicks ? ticks : user.QuestLengthTicks;
                user.QuestLengthSeconds = Math.Min(lengthSeconds, user.QuestLengthSeconds);
            }

            // Sync database
            database.UpdateUserAsync(user);

            // Create a new game if this was not the last level
            if (Level <= Parameters.FinalLevel)
            {
                currentGame = new Game(mapGenerator.Generate(Level), Parameters.MaxQuestInactivityTime);
                state = currentGame.State;
            }
            else
            {
                // TODO: Report to dashboard
                state = new GameState(ticks, Level, true);
            }
        }

        // Overwrite single game ticks with whole quest ticks
        state = state with { Tick = ticks };

        // Update visible state
        State = state;
    }

    public bool CheckIsActive() => currentGame.CheckIsActive();
}
