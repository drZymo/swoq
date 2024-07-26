using Swoq.Server.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Swoq.Server.Services;

public class SwoqDatabase : ISwoqDatabase
{
    private readonly IMongoCollection<Player> playersCollection;

    public SwoqDatabase(
        IOptions<SwoqDatabaseSettings> swoqDatabaseSettings)
    {
        var mongoClient = new MongoClient(
            swoqDatabaseSettings.Value.ConnectionString);

        var mongoDatabase = mongoClient.GetDatabase(
            swoqDatabaseSettings.Value.DatabaseName);

        playersCollection = mongoDatabase.GetCollection<Player>(
            swoqDatabaseSettings.Value.PlayersCollectionName);
    }

    public async Task<Player?> FindPlayerByNameAsync(string name) =>
        await playersCollection.Find(p => p.Name == name).FirstOrDefaultAsync();

    //public async Task<List<Player>> GetAllAsync() =>
    //    await playersCollection.Find(_ => true).ToListAsync();

    //public async Task<Player?> GetAsync(string id) =>
    //    await playersCollection.Find(x => x.Id == id).FirstOrDefaultAsync();

    public async Task CreatePlayerAsync(Player newPlayer) =>
        await playersCollection.InsertOneAsync(newPlayer);

    //public async Task UpdateAsync(string id, Player updatedPlayer) =>
    //    await playersCollection.ReplaceOneAsync(x => x.Id == id, updatedPlayer);

    //public async Task RemoveAsync(string id) =>
    //    await playersCollection.DeleteOneAsync(x => x.Id == id);
}