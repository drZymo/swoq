using System.Collections.Immutable;

namespace Swoq.Data;

public class SwoqDatabaseInMemory : ISwoqDatabase
{
    private readonly Lock usersWriteMutex = new();
    private IImmutableDictionary<string, User> users = ImmutableDictionary<string, User>.Empty;

    public SwoqDatabaseInMemory()
    { }

    public async Task CreateUserAsync(User newUser) =>
        await Task.Run(() =>
        {
            newUser.Id ??= Guid.NewGuid().ToString();
            lock (usersWriteMutex)
            {
                users = users.Add(newUser.Id, newUser);
            }
        });

    public void CreateUser(User newUser)
    {
        newUser.Id ??= Guid.NewGuid().ToString();
        lock (usersWriteMutex)
        {
            users = users.Add(newUser.Id, newUser);
        }
    }

    public async Task<User?> FindUserByIdAsync(string id) =>
        await Task.FromResult(users.TryGetValue(id, out var u) ? u : null);

    public async Task<User?> FindUserByNameAsync(string name) =>
        await Task.Run(() => users.Values.Where(u => u.Name.Equals(name)).FirstOrDefault());

    public async Task UpdateUserAsync(User user)
    {
        if (user.Id == null) return;
        await Task.Run(() => users = users.SetItem(user.Id, user));
    }

    public async Task<IImmutableList<User>> GetAllUsers() =>
        await Task.FromResult(users.Values.ToImmutableArray());
}
