using Swoq.Data;

namespace Swoq.Server;

class UserStatisticsReporter(User user, ISwoqDatabase database) : IStatisticsReporter
{
    public void GameFinishedSuccessfully(Guid gameId, int level, int ticks)
    {
        var stat = new LevelStatistic
        {
            UserId = user.Id ?? "",
            Level = level,
            Ticks = ticks,
        };
        database.AddLevelStatisticAsync(stat);
    }
}
