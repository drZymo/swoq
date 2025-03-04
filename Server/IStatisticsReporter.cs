namespace Swoq.Server;

interface IStatisticsReporter
{
    void GameFinishedSuccessfully(Guid gameId, int level, int ticks);
}
