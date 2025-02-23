using Microsoft.JSInterop;
using System.Threading.Tasks;

public class UserService
{
    private readonly IJSRuntime _jsRuntime;

    public UserService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task SetUserIdAsync(string userId)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "userId", userId);
    }

    public async Task<string> GetUserIdAsync()
    {
        return await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "userId");
    }

    public async Task RemoveUserIdAsync()
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "userId");
    }
}
