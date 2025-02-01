using Swoq.Data;
using Swoq.Infra;
using Swoq.Interface;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Swoq.Server;

public class GameServerException(Result result, GameState? state = null, Exception? innerException = null) : Exception(null, innerException)
{
    public Result Result { get; } = result;
    public GameState? State { get; } = state;
}

public class GameServer<MG>(ISwoqDatabase database, int nrActiveQuests = Parameters.NrOfActiveQuests) : IGameServer where MG : IMapGenerator
{
    private readonly ConcurrentDictionary<Guid, IGame> games = new();

    private readonly Lock startQuestMutex = new();
    private readonly ConcurrentDictionary<Guid, string> currentQuestUserIds = new();

    private readonly QuestQueue questQueue = new();

    public event EventHandler<GameRemovedEventArgs>? GameRemoved;

    public event EventHandler<QueueUpdatedEventArgs>? QueueUpdated
    {
        add => questQueue.Updated += value;
        remove => questQueue.Updated -= value;
    }

    public GameStartResult Start(string userId, int? level)
    {
        IGame? game = null;

        try
        {
            var user = GetUserOrThrow(database, userId);

            // First remove old games
            CleanupOldGames();

            // Create a new game
            game = level.HasValue ? StartTraining(user, level.Value) : StartQuest(user);
            games.TryAdd(game.Id, game);

            return new GameStartResult(user.Name, game.Id, game.State);
        }
        catch (SwoqException ex)
        {
            throw new GameServerException(ResultFromException(ex), game?.State, ex);
        }
        catch (Exception ex)
        {
            throw new GameServerException(Result.InternalError, game?.State, ex);
        }
    }

    private static User GetUserOrThrow(ISwoqDatabase database, string userId)
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
        return user;
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

        // We must prevent too many quests starting at the same time.
        // Due to multi-threading, two quests could try starting at the same time, exceeding the limit.
        // Therefore, we lock the whole start process.
        lock (startQuestMutex)
        {
            // Check that user is not already in a quest
            if (currentQuestUserIds.Any(kvp => kvp.Value == user.Id))
            {
                throw new QuestAlreadyActiveException();
            }

            // Queue (or update entry) user
            questQueue.Enqueue(user);

            // Do not allow starting another quest when too many quests are active.
            if (currentQuestUserIds.Count >= nrActiveQuests)
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
            currentQuestUserIds.TryAdd(quest.Id, user.Id);
            return quest;
        }
    }

    public GameState Act(Guid gameId, DirectedAction? action1 = null, DirectedAction? action2 = null)
    {
        // Try to find game
        if (!games.TryGetValue(gameId, out var game)) throw new GameServerException(Result.UnknownGameId, null);

        try
        {
            // Play game
            game.Act(action1, action2);

            return game.State;
        }
        catch (SwoqException ex)
        {
            throw new GameServerException(ResultFromException(ex), game?.State, ex);
        }
        catch (Exception ex)
        {
            throw new GameServerException(Result.InternalError, game?.State, ex);
        }
    }

    private void CleanupOldGames()
    {
        var now = Clock.Now;

        // Check status of all active games
        foreach (var game in games.Values.Where(g => !g.State.IsFinished))
        {
            game.CheckGameIsFinished();
        }

        // Remove games that are finished from active quests list
        var idsFinished = games.Values.
            Where(g => g.State.IsFinished).
            Select(g => g.Id).
            ToImmutableArray();

        if (idsFinished.Length > 0)
        {
            foreach (var id in idsFinished)
            {
                currentQuestUserIds.TryRemove(id, out _);
            }
        }

        // Remove games that have been finished for a while from the game list
        var idsToRemove = games.Values.
            Where(g => g.State.IsFinished).
            Where(g => now - g.LastActionTime > Parameters.GameRetentionTime).
            Select(g => g.Id).
            ToImmutableArray();

        if (idsToRemove.Length > 0)
        {
            foreach (var id in idsToRemove)
            {
                games.TryRemove(id, out var _);
                GameRemoved?.Invoke(this, new GameRemovedEventArgs(id));
            }
        }
    }

    private static Result ResultFromException(SwoqException ex) => ex switch
    {
        UnknownUserException => Result.UnknownUser,
        UnknownGameIdException => Result.UnknownGameId,
        UserLevelTooLowException => Result.UserLevelTooLow,
        QuestQueuedException => Result.QuestQueued,
        QuestAlreadyActiveException => Result.QuestAlreadyActive,
        MoveNotAllowedException => Result.MoveNotAllowed,
        UnknownActionException => Result.UnknownAction,
        GameFinishedException => Result.GameFinished,
        UseNotAllowedException => Result.UseNotAllowed,
        InventoryFullException => Result.InventoryFull,
        InventoryEmptyException => Result.InventoryEmpty,
        NoSwordException => Result.NoSword,
        Player1NotPresentException => Result.PlayerNotPresent,
        Player2NotPresentException => Result.Player2NotPresent,
        _ => Result.InternalError,
    };
}
