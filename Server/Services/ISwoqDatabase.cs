using Swoq.Server.Models;

namespace Swoq.Server.Services;

public interface ISwoqDatabase
{
    Task CreatePlayerAsync(Player newPlayer);
    Task<Player?> FindPlayerByIdAsync(string id);
    Task<Player?> FindPlayerByNameAsync(string name);
}