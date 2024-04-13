using Swoq.Server.Models;
using System.Collections.Immutable;

namespace Swoq.Server.Services;

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
}