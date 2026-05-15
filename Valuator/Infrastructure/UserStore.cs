using Microsoft.AspNetCore.Identity;
using StackExchange.Redis;

namespace Valuator.Infrastructure;

public class UserStore
{
    private readonly IDatabase _db;
    private readonly PasswordHasher<string> _hasher = new();

    public UserStore(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    public async Task<bool> RegisterAsync(string username, string password)
    {
        var key = $"USER-{username.ToLowerInvariant()}";
        var exists = await _db.KeyExistsAsync(key);
        if (exists) return false;

        var passwordHash = _hasher.HashPassword(username, password);
        await _db.HashSetAsync(key, new HashEntry[]
        {
            new("username", username),
            new("passwordHash", passwordHash),
            new("id", Guid.NewGuid().ToString())
        });

        return true;
    }

    public async Task<string?> ValidateAsync(string username, string password)
    {
        var key = $"USER-{username.ToLowerInvariant()}";
        var passwordHash = await _db.HashGetAsync(key, "passwordHash");
        if (passwordHash.IsNull) return null;

        var result = _hasher.VerifyHashedPassword(username, passwordHash!, password);
        if (result == PasswordVerificationResult.Success)
        {
            return await _db.HashGetAsync(key, "id");
        }

        return null;
    }
}
