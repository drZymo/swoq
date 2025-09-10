using Swoq.Data;

namespace Swoq.Server;

class UserStatisticsReporter(string userId, ISwoqDatabase database) : IStatisticsReporter
{
    public async void GameFinishedSuccessfully(Guid gameId, int level, int ticks)
    {
        var stat = new LevelStatistic
        {
            UserId = userId ?? "",
            Level = level,
            Ticks = ticks,
        };
        await database.AddLevelStatisticAsync(stat);
    }

    public async void QuestLevelReached(Guid gameId, int level, int ticks, int lengthSeconds)
    {
        var progress = new QuestProgress
        {
            UserId = userId ?? "",
            GameId = gameId.ToString(),
            Level = level,
            Ticks = ticks,
            Seconds = lengthSeconds,
        };
        await database.AddQuestProgressAsync(progress);
    }
}
