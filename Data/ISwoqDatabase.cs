using System.Collections.Immutable;

namespace Swoq.Data;

public interface ISwoqDatabase
{
    Task CreateUserAsync(User newUser);
    void CreateUser(User newUser);
    Task<User?> FindUserByIdAsync(string id);
    Task<User?> FindUserByNameAsync(string name);
    Task UpdateUserAsync(User user);

    Task<IImmutableList<User>> GetAllUsers();

    Task AddLevelStatisticAsync(LevelStatistic stat);
}
