using System.Collections.Concurrent;

namespace UiServiceTwo.Web.Services;

public interface IGlobalSessionStore
{
    void InvalidateUser(string userId);
    bool IsUserInvalid(string userId);
}

public class GlobalSessionStore : IGlobalSessionStore
{
    private readonly ConcurrentDictionary<string, byte> _invalidUsers = new();

    public void InvalidateUser(string userId)
    {
        _invalidUsers.TryAdd(userId, 0);
    }

    public bool IsUserInvalid(string userId)
    {
        return _invalidUsers.ContainsKey(userId);
    }
}
