using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Collections.Immutable;

namespace Swoq.Server.Data;

public class SwoqDatabase : ISwoqDatabase
{
    private readonly IMongoCollection<User> usersCollection;

    public SwoqDatabase(IOptions<SwoqDatabaseSettings> swoqDatabaseSettings)
    {
        var mongoClient = new MongoClient(
            swoqDatabaseSettings.Value.ConnectionString);

        var mongoDatabase = mongoClient.GetDatabase(
            swoqDatabaseSettings.Value.DatabaseName);

        usersCollection = mongoDatabase.GetCollection<User>(
            swoqDatabaseSettings.Value.UsersCollectionName);
    }

    public async Task CreateUserAsync(User newUser) =>
        await usersCollection.InsertOneAsync(newUser);

    public async Task<User?> FindUserByIdAsync(string id) =>
        await usersCollection.Find(p => p.Id == id).FirstOrDefaultAsync();

    public async Task<User?> FindUserByNameAsync(string name) =>
        await usersCollection.Find(u => u.Name == name).FirstOrDefaultAsync();

    public async Task UpdateUserAsync(User user)
    {
        if (user.Id == null) return;
        var filter = Builders<User>.Filter.
            Eq(u => u.Id, user.Id);
        var update = Builders<User>.Update.
            Set(u => u.Level, user.Level).
            Set(u => u.QuestLengthTicks, user.QuestLengthTicks).
            Set(u => u.QuestLengthSeconds, user.QuestLengthSeconds);
        await usersCollection.UpdateOneAsync(filter, update);
    }

    public async Task<IImmutableList<User>> GetAllUsers()
    {
        var users = await usersCollection.FindAsync(u => true);
        var usersList = await users.ToListAsync();
        return usersList.ToImmutableArray();
    }
}