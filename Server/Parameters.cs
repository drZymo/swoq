namespace Swoq.Server;

internal static class Parameters
{
    public const int MapHeight = 48;
    public const int MapWidth = 64;
    public const int PlayerVisibilityRange = 8;
    public const int EnemyVisibilityRange = 5;

    public const int PlayerHealth = 5;
    public const int EnemyHealth = 6;
    public const int EnemyDamage = 1;
    public const int BossHealth = 100;
    public const int BossDamage = 100;

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

    public const int NrOfActiveQuests = 2;

    public static readonly TimeSpan SessionsUpdatePeriod = TimeSpan.FromMilliseconds(250);
    public static readonly TimeSpan ScoresUpdatePeriod = TimeSpan.FromMilliseconds(500);
    public static readonly TimeSpan StatisticsUpdatePeriod = TimeSpan.FromMilliseconds(250);
}
