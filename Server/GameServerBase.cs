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

internal abstract class GameServerBase : IGameServer, IDisposable
{
    protected readonly IMapGenerator mapGenerator;
    protected readonly ISwoqDatabase database;

    protected readonly ConcurrentDictionary<Guid, IGame> games = new();

    private readonly CancellationTokenSource cancellation = new();
    private readonly Thread cleanupThread;

    public GameServerBase(IMapGenerator mapGenerator, ISwoqDatabase database)
    {
        this.mapGenerator = mapGenerator;
        this.database = database;

        cleanupThread = new Thread(new ThreadStart(CleanupThread));
        cleanupThread.Start();
    }

    public virtual void Dispose()
    {
        cancellation.Cancel();
        cleanupThread.Join();
    }

    public event EventHandler<GameRemovedEventArgs>? GameRemoved;
    public event EventHandler<QueueUpdatedEventArgs>? QueueUpdated;
    public event EventHandler<GameStatusChangedEventArgs>? GameStatusChanged;

    public GameStartResult Start(string userId, string userName, int? level, int? seed = null)
    {
        try
        {
            var user = GetUserOrThrow(database, userId, userName);

            // If seed is not given, use a random one.
            var actualSeed = seed ?? Random.Shared.Next();

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
            game.StatusChanged += OnGameStatusChanged;

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

    private void CleanupThread()
    {
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(50));

                // Remove games that have been finished for a while from the game list
                // Checking IsFinished could trigger a StatusChanged event.
                var now = Clock.Now;
                var idsToRemove = games.Values.
                    Where(g => g.IsFinished).
                    Where(g => (now - g.LastActionTime) > Parameters.GameRetentionTime).
                    Select(g => g.Id).
                    ToList();
                foreach (var gameId in idsToRemove)
                {
                    if (games.TryRemove(gameId, out var game))
                    {
                        game.StatusChanged -= OnGameStatusChanged;
                        OnGameRemoved(gameId);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Exit gracefully
        }
    }

    protected void OnGameRemoved(Guid gameId) => GameRemoved?.Invoke(this, new GameRemovedEventArgs(gameId));

    protected void OnQueueUpdated(IImmutableList<string> queuedUsers) => QueueUpdated?.Invoke(this, new QueueUpdatedEventArgs(queuedUsers));

    private void OnGameStatusChanged(object? sender, EventArgs args)
    {
        var game = sender as IGame;
        if (game == null) return;

        GameStatusChanged?.Invoke(this, new GameStatusChangedEventArgs(game.Id, game.State.Status));
    }
}
