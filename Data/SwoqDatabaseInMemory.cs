using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Swoq.Data;

public class SwoqDatabaseInMemory : ISwoqDatabase
{
    private readonly ConcurrentDictionary<string, User> users = new();
    private readonly ConcurrentDictionary<string, LevelStatistic> levelStatistics = new();
    private readonly ConcurrentDictionary<string, QuestProgress> questHistory = new();

    public SwoqDatabaseInMemory()
    { }

    public async Task CreateUserAsync(User newUser) =>
        await Task.Run(() =>
        {
            newUser.Id ??= Guid.NewGuid().ToString();
            users.TryAdd(newUser.Id, newUser);
        });

    public void CreateUser(User newUser)
    {
        newUser.Id ??= Guid.NewGuid().ToString();
        users.TryAdd(newUser.Id, newUser);
    }

    public async Task<User?> FindUserAsync(string id, string name) =>
        await Task.FromResult(users.TryGetValue(id, out var u) && u.Name == name ? u : null);

    public async Task UpdateUserAsync(User user)
    {
        await Task.Run(() => users.AddOrUpdate(user.Id, user, (_, _) => user));
    }

    public async Task<IImmutableList<User>> GetAllUsers() =>
        await Task.FromResult(users.Values.ToImmutableArray());

    public async Task AddLevelStatisticAsync(LevelStatistic stat)
    {
        await Task.Run(() =>
        {
            stat.Id ??= Guid.NewGuid().ToString();
            levelStatistics.TryAdd(stat.Id, stat);
        });
    }

    public Task<IImmutableList<UserLevelStatistic>> GetLevelStatisticsAsync(string userId)
    {
        throw new NotImplementedException();
    }

    public async Task AddQuestProgressAsync(QuestProgress progress)
    {
        await Task.Run(() =>
        {
            progress.Id ??= Guid.NewGuid().ToString();
            questHistory.TryAdd(progress.Id, progress);
        });
    }

    public async Task<IImmutableList<UserQuestProgress>> GetLatestQuestHistory(int count) =>
        await Task.FromResult(questHistory.Values.
            GroupBy(q => q.GameId).
            Select(g => g.OrderByDescending(q => q.Timestamp).First()).
            OrderByDescending(q => q.Timestamp).
            Take(count).
            Select(q => new UserQuestProgress(users[q.UserId].Name, q.GameId, q.Level, q.Ticks, q.Seconds, q.Timestamp)).
            ToImmutableArray());
}
