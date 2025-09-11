using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Collections.Immutable;

namespace Swoq.Data;

public class SwoqDatabase : ISwoqDatabase
{
    private readonly IMongoCollection<User> users;
    private readonly IMongoCollection<LevelStatistic> levelStatistics;
    private readonly IMongoCollection<QuestProgress> questHistory;

    public SwoqDatabase(IOptions<SwoqDatabaseSettings> swoqDatabaseSettings)
    {
        var mongoClient = new MongoClient(
            swoqDatabaseSettings.Value.ConnectionString);

        var mongoDatabase = mongoClient.GetDatabase(
            swoqDatabaseSettings.Value.DatabaseName);

        users = mongoDatabase.GetCollection<User>(
            swoqDatabaseSettings.Value.UsersCollectionName);

        levelStatistics = mongoDatabase.GetCollection<LevelStatistic>(
            swoqDatabaseSettings.Value.LevelStatisticsCollectionName);

        questHistory = mongoDatabase.GetCollection<QuestProgress>(
            swoqDatabaseSettings.Value.QuestHistoryCollectionName);
    }

    public async Task CreateUserAsync(User newUser) =>
        await users.InsertOneAsync(newUser);

    public void CreateUser(User newUser) =>
        users.InsertOne(newUser);

    public async Task<User?> FindUserAsync(string id, string name) =>
        await users.Find(u => u.Id == id && u.Name == name).FirstOrDefaultAsync();

    public async Task UpdateUserAsync(User user)
    {
        var filter = Builders<User>.Filter.
            Eq(u => u.Id, user.Id);
        var update = Builders<User>.Update.
            Set(u => u.Level, user.Level).
            Set(u => u.QuestLengthTicks, user.QuestLengthTicks).
            Set(u => u.QuestLengthSeconds, user.QuestLengthSeconds).
            Set(u => u.QuestFinished, user.QuestFinished).
            Set(u => u.BestQuestId, user.BestQuestId);
        await users.UpdateOneAsync(filter, update);
    }

    public async Task<IImmutableList<User>> GetAllUsers()
    {
        var users = await this.users.FindAsync(u => true);
        var usersList = await users.ToListAsync();
        return usersList.ToImmutableArray();
    }

    public async Task AddLevelStatisticAsync(LevelStatistic stat)
    {
        await levelStatistics.InsertOneAsync(stat);
    }

    public async Task<IImmutableList<UserLevelStatistic>> GetLevelStatisticsAsync(string userId)
    {
        var globalPipeLine = new EmptyPipelineDefinition<LevelStatistic>().
            Group(l => l.Level, g => new ValueTuple<int, int, double>(g.Key, g.Min(l => l.Ticks), g.Average(l => l.Ticks)));
        var globalResults = await levelStatistics.Aggregate(globalPipeLine).ToListAsync();
        var globalMin = globalResults.ToImmutableDictionary(r => r.Item1, r => (r.Item2, r.Item3));

        var userPipeLine = new EmptyPipelineDefinition<LevelStatistic>().
            Match(l => l.UserId == userId).
            Group(l => l.Level, g => new ValueTuple<int, int, double>(g.Key, g.Min(l => l.Ticks), g.Average(l => l.Ticks)));
        var userResults = await levelStatistics.Aggregate(userPipeLine).ToListAsync();
        var userStats = userResults.ToImmutableDictionary(r => r.Item1, r => (r.Item2, r.Item3));

        return userStats.
            Select(kvp => new UserLevelStatistic(
                kvp.Key,
                kvp.Value.Item1,
                globalMin[kvp.Key].Item1,
                (int)Math.Round(kvp.Value.Item2, MidpointRounding.AwayFromZero),
                (int)Math.Round(globalMin[kvp.Key].Item2, MidpointRounding.AwayFromZero))).
            ToImmutableArray();
    }

    public async Task AddQuestProgressAsync(QuestProgress progress)
    {
        await questHistory.InsertOneAsync(progress);
    }

    public async Task<IImmutableList<UserQuestProgress>> GetLatestQuestHistory(int count, string? userId)
    {
        var filter = string.IsNullOrEmpty(userId)
            ? Builders<QuestProgress>.Filter.Empty
            : Builders<QuestProgress>.Filter.Eq(q => q.UserId, userId);
        var sort = Builders<QuestProgress>.Sort.Descending(q => q.Timestamp);

        // Group by GameId and select the newest entry for each GameId
        var latestByGameId = await questHistory.Aggregate().
            Match(filter).
            Sort(sort).
            Group(q => q.GameId, g => new QuestProgress
            {
                UserId = g.First().UserId,
                GameId = g.Key,
                Level = g.First().Level,
                Ticks = g.First().Ticks,
                Seconds = g.First().Seconds,
                Timestamp = g.First().Timestamp,
            }).
            Sort(sort).
            Limit(count).ToListAsync();

        // Find corresponding users
        var userIds = latestByGameId.Select(q => q.UserId).ToHashSet();
        var users = (await this.users.Find(u => userIds.Contains(u.Id)).ToListAsync())
            .ToDictionary(u => u.Id, u => u);

        // Combine
        return latestByGameId
            .Select(q => new UserQuestProgress(
                users.TryGetValue(q.UserId, out var user) ? user.Name : "<unknown user>",
                q.GameId, q.Level, q.Ticks, q.Seconds, q.Timestamp))
            .ToImmutableArray();
    }
}
