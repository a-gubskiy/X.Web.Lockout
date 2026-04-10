using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;

namespace X.Web.Lockout.Services;

public class DistributedLockoutService : LockoutServiceBase
{
    private const string KeyPrefix = "lockout";

    private readonly IDistributedCache _cache;

    public DistributedLockoutService(LockoutOptions options, IDistributedCache cache)
        : this(options, TimeProvider.System, cache)
    {
    }

    public DistributedLockoutService(LockoutOptions options, TimeProvider timeProvider, IDistributedCache cache)
        : base(options, timeProvider)
    {
        _cache = cache;
    }

    protected override async Task<LockoutEntry?> LoadAsync(string userId, CancellationToken cancellationToken)
    {
        var json = await _cache.GetStringAsync(BuildKey(userId), cancellationToken);

        if (json is null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<LockoutEntry>(json);
    }

    protected override async Task SaveAsync(string userId, LockoutEntry entry, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(entry);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = GetEntryLifetime(entry)
        };

        await _cache.SetStringAsync(BuildKey(userId), json, options, cancellationToken);
    }

    protected override async Task RemoveAsync(string userId, CancellationToken cancellationToken)
    {
        await _cache.RemoveAsync(BuildKey(userId), cancellationToken);
    }

    private static string BuildKey(string userId) => $"{KeyPrefix}:{userId}";
}
