using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Collections.Immutable;

namespace Swoq.Data;

public class SwoqDatabase : ISwoqDatabase
{
    private readonly IMongoCollection<User> users;
    private readonly IMongoCollection<LevelStatistic> levelStatistics;

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
    }

    public async Task CreateUserAsync(User newUser) =>
        await users.InsertOneAsync(newUser);

    public void CreateUser(User newUser) =>
        users.InsertOne(newUser);

    public async Task<User?> FindUserAsync(string id, string name) =>
        await users.Find(u => u.Id == id && u.Name == name).FirstOrDefaultAsync();

    public async Task UpdateUserAsync(User user)
    {
        if (user.Id == null) return;
        var filter = Builders<User>.Filter.
            Eq(u => u.Id, user.Id);
        var update = Builders<User>.Update.
            Set(u => u.Level, user.Level).
            Set(u => u.QuestLengthTicks, user.QuestLengthTicks).
            Set(u => u.QuestLengthSeconds, user.QuestLengthSeconds);
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

    public async Task<ImmutableList<UserLevelStatistic>> GetLevelStatisticsAsync(string userId)
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
            ToImmutableList();
    }
}
