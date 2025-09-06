using Swoq.Data;
using Swoq.Infra;
using Swoq.Interface;
using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Swoq.Server;

public class GameServerStartException(StartResult result, Exception? innerException = null) : Exception($"Start result {result}", innerException)
{
    public StartResult Result { get; } = result;
}

public class GameServerActException(ActResult result, GameState? state, Exception? innerException = null) : Exception($"Act result {result}", innerException)
{
    public ActResult Result { get; } = result;
    public GameState? State { get; } = state;
}

internal abstract class GameServerBase(IMapGenerator mapGenerator, ISwoqDatabase database) : IGameServer
{
    protected readonly IMapGenerator mapGenerator = mapGenerator;
    protected readonly ISwoqDatabase database = database;

    protected readonly ConcurrentDictionary<Guid, IGame> games = new();

    public event EventHandler<GameRemovedEventArgs>? GameRemoved;

    public event EventHandler<QueueUpdatedEventArgs>? QueueUpdated;

    public GameStartResult Start(string userId, string userName, int? level, int? seed = null)
    {
        try
        {
            var user = GetUserOrThrow(database, userId, userName);

            // If seed is not given, use a random one.
            var actualSeed = seed ?? Random.Shared.Next();

            // Cleanup
            CleanupOldGames();

            // Create a new game
            IGame game;
            if (level.HasValue)
            {
                // Check if user can play this level
                if (level < 0 || level > user.Level || level > mapGenerator.MaxLevel) throw new InvalidLevelException();
                game = StartTraining(user, level.Value, ref actualSeed);
            }
            else
            {
                var quest = StartQuest(user);
                actualSeed = quest.Seed;
                game = quest;
            }

            if (!games.TryAdd(game.Id, game))
            {
                throw new InvalidOperationException("Game could not be added");
            }

            return new GameStartResult(user.Name, game.Id, game.State, actualSeed);
        }
        catch (SwoqStartException ex)
        {
            throw new GameServerStartException(ex.Result, ex);
        }
        catch (Exception ex)
        {
            throw new GameServerStartException(StartResult.InternalError, ex);
        }
    }

    private static User GetUserOrThrow(ISwoqDatabase database, string userId, string userName)
    {
        try
        {
            return database.FindUserAsync(userId, userName).Result ?? throw new UnknownUserException();
        }
        catch
        {
            throw new UnknownUserException();
        }
    }

    protected abstract Game StartTraining(User user, int level, ref int seed);

    protected abstract Quest StartQuest(User user);

    public GameState Act(Guid gameId, DirectedAction? action1 = null, DirectedAction? action2 = null)
    {
        // Try to find game
        if (!games.TryGetValue(gameId, out var game)) throw new GameServerActException(ActResult.UnknownGameId, null);

        // Play game
        try
        {
            game.Act(action1, action2);
            return game.State;
        }
        catch (SwoqActException ex)
        {
            throw new GameServerActException(ex.Result, game.State, ex);
        }
        catch (Exception ex)
        {
            throw new GameServerActException(ActResult.InternalError, game.State, ex);
        }
    }

    private void CleanupOldGames()
    {
        // Remove games that have been finished for a while from the game list
        var now = Clock.Now;
        var idsToRemove = games.Values.
            Where(g => g.IsFinished && (now - g.LastActionTime) > Parameters.GameRetentionTime).
            Select(g => g.Id).
            ToList();
        foreach (var id in idsToRemove)
        {
            RemoveGame(id);
        }
    }

    protected void RemoveGame(Guid gameId)
    {
        if (games.TryRemove(gameId, out var game))
        {
            GameRemoved?.Invoke(this, new GameRemovedEventArgs(gameId));
        }
    }

    protected void OnQueueUpdated(IImmutableList<string> queuedUsers)
    {
        QueueUpdated?.Invoke(this, new QueueUpdatedEventArgs(queuedUsers));
    }
}
