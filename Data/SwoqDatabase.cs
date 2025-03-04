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

    public async Task<User?> FindUserByIdAsync(string id) =>
        await users.Find(p => p.Id == id).FirstOrDefaultAsync();

    public async Task<User?> FindUserByNameAsync(string name) =>
        await users.Find(u => u.Name == name).FirstOrDefaultAsync();

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
}
