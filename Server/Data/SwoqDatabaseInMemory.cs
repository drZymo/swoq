using Swoq.Server.Data;
using System.Collections.Immutable;

namespace Swoq.Server.Data;

public class SwoqDatabaseInMemory : ISwoqDatabase
{
    private readonly object playersWriteMutex = new();
    private IImmutableDictionary<string, Player> players = ImmutableDictionary<string, Player>.Empty;

    public SwoqDatabaseInMemory()
    { }

    public async Task CreatePlayerAsync(Player newPlayer) =>
        await Task.Run(() =>
        {
            newPlayer.Id ??= Guid.NewGuid().ToString();
            lock (playersWriteMutex)
            {
                players = players.Add(newPlayer.Id, newPlayer);
            }
        });

    public async Task<Player?> FindPlayerByIdAsync(string id) =>
        await Task.FromResult(players.TryGetValue(id, out var p) ? p : null);

    public async Task<Player?> FindPlayerByNameAsync(string name) =>
        await Task.Run(() => players.Values.Where(p => p.Name.Equals(name)).FirstOrDefault());

    public async Task UpdatePlayerAsync(Player player)
    {
        if (player.Id == null) return;
        await Task.Run(() => players = players.SetItem(player.Id, player));
    }

    public async Task<IImmutableList<Player>> GetAllPlayers() =>
        await Task.FromResult(players.Values.ToImmutableArray());
}
