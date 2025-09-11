namespace Swoq.Server;

public interface IStatisticsReporter
{
    void GameFinishedSuccessfully(Guid gameId, int level, int ticks);
    void QuestLevelReached(Guid gameId, int level, int ticks, int lengthSeconds);
}
