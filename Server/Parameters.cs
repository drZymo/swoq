namespace Swoq.Server;

internal static class Parameters
{
    public const int MapHeight = 48;
    public const int MapWidth = 64;
    public const int PlayerVisibilityRange = 8;
    public const int EnemyVisibilityRange = 5;

    public const int ExtraHealth = 3;

    public static readonly TimeSpan GameRetentionTime = TimeSpan.FromSeconds(30);

    public static readonly TimeSpan MaxTrainingInactivityTime = TimeSpan.FromSeconds(10);

    public static readonly TimeSpan MaxQuestInactivityTime = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Maximum number of ticks that a game is allowed to not have progress, i.e. nothing changed on the map.
    /// </summary>
    public const int MaxNoProgressTicks = 1000;

    /// <summary>
    /// Maximum number of ticks that a player is allowed to not move around, i.e. player is idle.
    /// </summary>
    public const int MaxInactivityTicks = 500;

    public const int MinIdleMoveDistance = 5;

    /// <summary>
    /// How many quests can be active at the same time.
    /// </summary>
    public const int NrOfActiveQuests = 1;

    /// <summary>
    /// Period between sanity checks of the queue
    /// </summary>
    public static readonly TimeSpan QueuePollPeriod = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// How long does the quest start function wait for the queue before returning "quest queued"
    /// </summary>
    public static readonly TimeSpan QueueWaitTime = TimeSpan.FromSeconds(3);

    public static readonly TimeSpan SessionsUpdatePeriod = TimeSpan.FromMilliseconds(250);
    public static readonly TimeSpan ScoresUpdatePeriod = TimeSpan.FromMilliseconds(500);
    public static readonly TimeSpan StatisticsUpdatePeriod = TimeSpan.FromMilliseconds(250);

    public const int MaxLevelTicks = 10000;
    public static readonly TimeSpan MaxLevelDuration = TimeSpan.FromSeconds(120);

    /// <summary>
    /// The maximum ticks per second allowed during final mode
    /// </summary>
    public const int FinalTickRate = 30;
}
