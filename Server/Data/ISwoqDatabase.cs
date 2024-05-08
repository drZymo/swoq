using Swoq.Server.Data;
using System.Collections.Immutable;

namespace Swoq.Server.Data;

public interface ISwoqDatabase
{
    Task CreatePlayerAsync(Player newPlayer);
    Task<Player?> FindPlayerByIdAsync(string id);
    Task<Player?> FindPlayerByNameAsync(string name);
    Task UpdatePlayerAsync(Player player);

    Task<IImmutableList<Player>> GetAllPlayers();
}