using Swoq.Infra;
using Swoq.Interface;
using Swoq.Server.Data;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Swoq.Server;

public class GameServer<MG>(ISwoqDatabase database, int nrActiveQuests = Parameters.NrOfActiveQuests) : IGameServer where MG : IMapGenerator
{
    private readonly object gamesWriteMutex = new();
    private IImmutableDictionary<Guid, IGame> games = ImmutableDictionary<Guid, IGame>.Empty;

    private readonly object currentQuestMutex = new();
    private ImmutableHashSet<Guid> currentQuestIds = [];

    private readonly QuestQueue questQueue = new();

    public event EventHandler<IImmutableList<string>>? QueueUpdated
    {
        add => questQueue.Updated += value;
        remove => questQueue.Updated -= value;
    }

    public event EventHandler<(Guid gameId, string username, int? level)>? GameAdded;
    public event EventHandler<Guid>? GameRemoved;
    public event EventHandler<(Guid gameId, bool finished)>? GameActed;

    public GameStartResult Start(string userId, int? level)
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
            GameAdded?.Invoke(this, (game.Id, user.Name, level));
        }
        // and remove old games
        CleanupOldGames();

        return new GameStartResult(user.Name, game.Id, game.State);
    }

    private static Game StartTraining(User user, int level)
    {
        // Check if user can play this level
        if (level < 0 || user.Level < level) throw new UserLevelTooLowException();

        var map = MG.Generate(level, Parameters.MapHeight, Parameters.MapWidth);

        // Create new training game
        return new Game(map, Parameters.MaxTrainingInactivityTime);
    }

    private Quest<MG> StartQuest(User user)
    {
        if (user.Id == null) throw new ArgumentNullException(nameof(user));

        lock (currentQuestMutex)
        {
            var now = Clock.Now;

            // Cleanup current quest if finished or idle for too long
            foreach (var currentQuestId in currentQuestIds)
            {
                var currentQuest = games[currentQuestId];
                if (currentQuest.State.Finished || !currentQuest.CheckIsActive())
                {
                    currentQuestIds = currentQuestIds.Remove(currentQuestId);
                }
            }

            // Queue (or update entry) user
            questQueue.Enqueue(user);

            // Do not allow starting another quest when too many quests are active.
            if (currentQuestIds.Count >= nrActiveQuests)
            {
                throw new QuestQueuedException();
            }

            // Is this user the first entry in the queue?
            if (questQueue.FrontUserId != user.Id)
            {
                throw new QuestQueuedException();
            }

            // No quests active
            // User first in queue
            // Dequeue
            var (frontUserId, frontUserName) = questQueue.Dequeue();
            Debug.Assert(frontUserId == user.Id);
            Debug.Assert(frontUserName == user.Name);
            // and start a new game
            var quest = new Quest<MG>(user, database);
            currentQuestIds = currentQuestIds.Add(quest.Id);
            return quest;
        }
    }

    public GameState Act(Guid gameId, DirectedAction? action1 = null, DirectedAction? action2 = null)
    {
        // Does game exist?
        if (!games.TryGetValue(gameId, out var game)) throw new UnknownGameIdException();

        // Play game
        game.Act(action1, action2);
        GameActed?.Invoke(this, (gameId, game.State.Finished));
        return game.State;
    }

    private void CleanupOldGames()
    {
        var now = Clock.Now;

        // Find games that have been finished for a while
        var idsToRemove = games.Values.
            Where(g => now - g.LastActionTime > Parameters.GameRetentionTime).
            Where(g => g.State.Finished || !g.CheckIsActive()).
            Select(g => g.Id).
            ToImmutableArray();

        if (idsToRemove.Length > 0)
        {
            // Update current quest
            lock (currentQuestMutex)
            {
                foreach (var gameId in idsToRemove)
                {
                    currentQuestIds = currentQuestIds.Remove(gameId);
                }
            }
            // Before actually removing the games
            lock (gamesWriteMutex)
            {
                games = games.RemoveRange(idsToRemove);
            }
            // Notify monitors
            foreach (var id in idsToRemove)
            {
                GameRemoved?.Invoke(this, id);
            }
        }
    }
}
