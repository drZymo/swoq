using System.Collections.Immutable;

namespace Swoq.Data;

public record UserLevelStatistic(int Level, int MinTicks, int GlobalMinTicks, int AvgTicks, int GlobalAvgTicks)
{
    public int DeltaMin => MinTicks - GlobalMinTicks;
    public int DeltaAvg => AvgTicks - GlobalAvgTicks;
}

public interface ISwoqDatabase
{
    Task CreateUserAsync(User newUser);
    void CreateUser(User newUser);
    Task<User?> FindUserAsync(string id, string name);
    Task UpdateUserAsync(User user);

    Task<IImmutableList<User>> GetAllUsers();

    Task AddLevelStatisticAsync(LevelStatistic stat);
    Task<ImmutableList<UserLevelStatistic>> GetLevelStatisticsAsync(string userId);
}
