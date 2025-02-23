using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Swoq.Data;

public class UserService(ISwoqDatabase database, ProtectedLocalStorage localStorage)
{
    public async Task SetUserIdAsync(string userId)
    {
        await localStorage.SetAsync("userId", userId);
    }

    public async Task<User?> GetCurrentUserAsync()
    {
        try
        {
            var userId = await localStorage.GetAsync<string>("userId");
            if (!userId.Success || userId.Value == null) return null;

            var user = await database.FindUserByIdAsync(userId.Value);
            return user;
        }
        catch
        {
            return null;
        }
    }

    public async Task RemoveUserIdAsync()
    {
        await localStorage.DeleteAsync("userId");
    }
}
