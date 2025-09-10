using Swoq.Data;
using Swoq.Infra;

namespace Swoq.Server;

internal class GameServer : GameServerBase
{
    private readonly QueueManager queueManager;

    public GameServer(IMapGenerator mapGenerator, ISwoqDatabase database, int? maxNrActiveQuests = null, TimeSpan? queueWaitTime = null)
        : base(mapGenerator, database)
    {
        queueManager = new(mapGenerator, database, maxNrActiveQuests, queueWaitTime);
        queueManager.QueueUpdated += OnQueueManagerQueueUpdated;
    }

    public override void Dispose()
    {
        queueManager.QueueUpdated -= OnQueueManagerQueueUpdated;
        queueManager.Dispose();

        base.Dispose();
    }

    protected override Game StartTraining(User user, int level, ref int seed)
    {
        // Check if user can play this level
        if (level < 0 || level > user.Level || level > mapGenerator.MaxLevel) throw new InvalidLevelException();

        var random = new Random(seed + level);
        var map = mapGenerator.Generate(level, Parameters.MapHeight, Parameters.MapWidth, random);
        var reporter = new UserStatisticsReporter(user, database);

        // Create new training game
        return new Game(
            map,
            Parameters.MaxTrainingInactivityTime,
            Parameters.MaxLevelTicks,
            Parameters.MaxLevelDuration,
            random,
            reporter);
    }

    protected override Quest StartQuest(User user)
    {
        try
        {
            var quest = queueManager.TryStartQuest(user);
            return quest ?? throw new QuestQueuedException();
        }
        catch (OperationCanceledException)
        {
            // Tell user quest is not allowed at this moment.
            throw new NotAllowedException();
        }
    }

    private void OnQueueManagerQueueUpdated(object? sender, QueueUpdatedEventArgs args) => OnQueueUpdated(args.QueuedUsers);
}
