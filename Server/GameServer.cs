using Swoq.Infra;
using Swoq.Interface;
using Swoq.Server.Data;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Swoq.Server;

public class GameServer(ISwoqDatabase database, IMapGenerator mapGenerator)
{
    public record StartResult(string UserName, Guid GameId, GameState State);

    private readonly object gamesWriteMutex = new();
    private IImmutableDictionary<Guid, IGame> games = ImmutableDictionary<Guid, IGame>.Empty;

    private readonly object currentQuestMutex = new();
    private Guid? currentQuestId = null;

    private readonly QuestQueue questQueue = new();

    public event EventHandler<IImmutableList<string>>? QueueUpdated
    {
        add => questQueue.Updated += value;
        remove => questQueue.Updated -= value;
    }

    public StartResult Start(string userId, int? level)
    {
        User user;
        try
        {
            // Get user object
            user = database.FindUserByIdAsync(userId).Result ?? throw new UnknownUserException();
        }
        catch
        {
            throw new UnknownUserException();
        }

        // Create a new game
        IGame game = level.HasValue ? StartTraining(user, level.Value) : StartQuest(user);
        lock (gamesWriteMutex)
        {
            games = games.Add(game.Id, game);
        }
        // and remove old games
        CleanupOldGames();

        return new StartResult(user.Name, game.Id, game.State);
    }

    private Game StartTraining(User user, int level)
    {
        // Check if user can play this level
        if (level < 0 || user.Level < level) throw new UserLevelTooLowException();

        var map = mapGenerator.Generate(level);

        // Create new training game
        return new Game(map, Parameters.MaxTrainingInactivityTime);
    }

    private Quest StartQuest(User user)
    {
        if (user.Id == null) throw new ArgumentNullException(nameof(user));

        lock (currentQuestMutex)
        {
            var now = Clock.Now;

            // Cleanup current quest if finished or idle for too long
            if (currentQuestId.HasValue)
            {
                var currentQuest = games[currentQuestId.Value];
                if (currentQuest.State.Finished || !currentQuest.CheckIsActive())
                {
                    currentQuestId = null;
                }
            }

            // Queue (or update entry) user
            questQueue.Enqueue(user);

            // Do not allow starting another quest when current quest is active.
            if (currentQuestId.HasValue)
            {
                throw new QuestQueuedException();
            }

            // Is this user the first entry in the queue?
            if (questQueue.FrontUserId != user.Id)
            {
                throw new QuestQueuedException();
            }
            var (frontUserId, frontUserName) = questQueue.Dequeue();
            Debug.Assert(frontUserId == user.Id);
            Debug.Assert(frontUserName == user.Name);

            // No quest active
            // User first in queue
            // Start a new game
            var quest = new Quest(user, database, mapGenerator);
            currentQuestId = quest.Id;
            return quest;
        }
    }

    public GameState Act(Guid gameId, DirectedAction? action1 = null, DirectedAction? action2 = null)
    {
        // Does game exist?
        if (!games.TryGetValue(gameId, out var game)) throw new UnknownGameIdException();

        // Play game
        game.Act(action1, action2);
        return game.State;
    }

    private void CleanupOldGames()
    {
        var now = Clock.Now;

        // Gather ids to remove
        var idsToRemove = games.Values.
            Where(g => now - g.LastActionTime > Parameters.GameRetentionTime).
            Select(g => g.Id).
            ToImmutableArray();

        // Remove all at once
        if (idsToRemove.Length > 0)
        {
            lock (gamesWriteMutex)
            {
                games = games.RemoveRange(idsToRemove);

                if (currentQuestId.HasValue && !games.ContainsKey(currentQuestId.Value))
                {
                    currentQuestId = null;
                }
            }
        }
    }
}
