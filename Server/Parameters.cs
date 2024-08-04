namespace Swoq.Server;

internal static class Parameters
{
    public const int MapHeight = 64;
    public const int MapWidth = 64;
    public const int PlayerVisibilityRange = 8;
    public const int EnemyVisibilityRange = 5;

    public const int PlayerHealth = 5;
    public const int EnemyHealth = 6;
    public const int EnemyDamage = 1;
    public const int BossHealth = 100;
    public const int BossDamage = 100;

    public const int ExtraHealth = 3;

    public const int FinalLevel = 20;

    public static readonly TimeSpan GameRetentionTime = TimeSpan.FromMinutes(10);

    public static readonly TimeSpan MaxTrainingInactivityTime = TimeSpan.FromMinutes(1);

    public static readonly TimeSpan MaxQuestInactivityTime = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Maximum number of ticks that a game is allowed to not have progress, i.e. player is idle.
    /// </summary>
    public const int MaxIdleTicks = 500;

    public const int MinIdleMoveDistance = 5;
}
