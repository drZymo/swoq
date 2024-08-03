using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Collections.Immutable;

namespace Swoq.Server.Data;

public class SwoqDatabase : ISwoqDatabase
{
	private readonly IMongoCollection<Player> playersCollection;

	public SwoqDatabase(IOptions<SwoqDatabaseSettings> swoqDatabaseSettings)
	{
		var mongoClient = new MongoClient(
			swoqDatabaseSettings.Value.ConnectionString);

		var mongoDatabase = mongoClient.GetDatabase(
			swoqDatabaseSettings.Value.DatabaseName);

		playersCollection = mongoDatabase.GetCollection<Player>(
			swoqDatabaseSettings.Value.PlayersCollectionName);
	}

	public async Task CreatePlayerAsync(Player newPlayer) =>
		await playersCollection.InsertOneAsync(newPlayer);

	public void CreatePlayer(Player newPlayer) =>
		playersCollection.InsertOne(newPlayer);

	public async Task<Player?> FindPlayerByIdAsync(string id) =>
		await playersCollection.Find(p => p.Id == id).FirstOrDefaultAsync();

	public async Task<Player?> FindPlayerByNameAsync(string name) =>
		await playersCollection.Find(p => p.Name == name).FirstOrDefaultAsync();

	public async Task UpdatePlayerAsync(Player player)
	{
		if (player.Id == null) return;
		var filter = Builders<Player>.Filter.
			Eq(p => p.Id, player.Id);
		var update = Builders<Player>.Update.
			Set(p => p.Level, player.Level).
			Set(p => p.QuestLengthTicks, player.QuestLengthTicks).
			Set(p => p.QuestLengthSeconds, player.QuestLengthSeconds);
		await playersCollection.UpdateOneAsync(filter, update);
	}

	public async Task<IImmutableList<Player>> GetAllPlayers()
	{
		var players = await playersCollection.FindAsync(p => true);
		var playersList = await players.ToListAsync();
		return playersList.ToImmutableArray();
	}
}