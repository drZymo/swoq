using System.Collections.Immutable;

namespace Swoq.Server.Data;

public interface ISwoqDatabase
{
    Task CreatePlayerAsync(Player newPlayer);
    void CreatePlayer(Player newPlayer);
    Task<Player?> FindPlayerByIdAsync(string id);
    Task<Player?> FindPlayerByNameAsync(string name);
    Task UpdatePlayerAsync(Player player);

    Task<IImmutableList<Player>> GetAllPlayers();
}