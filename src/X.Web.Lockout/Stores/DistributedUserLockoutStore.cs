using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using X.Web.Lockout.Internal;

namespace X.Web.Lockout.Stores;

public class DistributedUserLockoutStore<TUser> : UserLockoutStoreBase<TUser> where TUser : class
{
    private const string LockoutPrefix = "lockout";
    private const string LockoutEnabledPrefix = "lockout-enabled";
    private static readonly AsyncKeyedLock UserOperations = new();

    private readonly IDistributedCache _cache;

    public DistributedUserLockoutStore(IDistributedCache cache, IUserStore<TUser> userStore) : base(userStore)
    {
        _cache = cache;
    }

    public override async Task<DateTimeOffset?> GetLockoutEndDateAsync(TUser user, CancellationToken cancellationToken)
    {
        var userId = await GetUserIdAsync(user, cancellationToken);

        return (await GetLockoutEntryAsync(userId, cancellationToken)).LockoutEndDate;
    }

    public override async Task SetLockoutEndDateAsync(
        TUser user,
        DateTimeOffset? lockoutEnd,
        CancellationToken cancellationToken)
    {
        var userId = await GetUserIdAsync(user, cancellationToken);

        using (await UserOperations.AcquireAsync(userId, cancellationToken))
        {
            var info = await GetLockoutEntryAsync(userId, cancellationToken);
            info.LockoutEndDate = lockoutEnd;

            await SaveLockoutEntryAsync(userId, info, cancellationToken);
        }
    }

    public override async Task<int> IncrementAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
    {
        var userId = await GetUserIdAsync(user, cancellationToken);

        using (await UserOperations.AcquireAsync(userId, cancellationToken))
        {
            var info = await GetLockoutEntryAsync(userId, cancellationToken);
            info.AccessFailedCount++;

            await SaveLockoutEntryAsync(userId, info, cancellationToken);

            return info.AccessFailedCount;
        }
    }

    public override async Task ResetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
    {
        var userId = await GetUserIdAsync(user, cancellationToken);

        using (await UserOperations.AcquireAsync(userId, cancellationToken))
        {
            var info = await GetLockoutEntryAsync(userId, cancellationToken);
            info.AccessFailedCount = 0;

            await SaveLockoutEntryAsync(userId, info, cancellationToken);
        }
    }

    public override async Task<int> GetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
    {
        var userId = await GetUserIdAsync(user, cancellationToken);

        return (await GetLockoutEntryAsync(userId, cancellationToken)).AccessFailedCount;
    }

    public override async Task<bool> GetLockoutEnabledAsync(TUser user, CancellationToken cancellationToken)
    {
        var userId = await GetUserIdAsync(user, cancellationToken);
        var key = GetCacheKey(userId, LockoutEnabledPrefix);

        // Presence of the key is the signal — SetLockoutEnabledAsync only writes
        // for enabled=true and removes the key for enabled=false.
        return await _cache.GetStringAsync(key, cancellationToken) is not null;
    }

    public override async Task SetLockoutEnabledAsync(TUser user, bool enabled, CancellationToken cancellationToken)
    {
        var userId = await GetUserIdAsync(user, cancellationToken);
        var key = GetCacheKey(userId, LockoutEnabledPrefix);

        using (await UserOperations.AcquireAsync(userId, cancellationToken))
        {
            if (enabled)
            {
                await _cache.SetStringAsync(key, bool.TrueString, cancellationToken);
                return;
            }

            await _cache.RemoveAsync(key, cancellationToken);
        }
    }

    private static string GetCacheKey(string userId, string prefix) => $"{prefix}:{userId}";

    private async Task<LockoutEntry> GetLockoutEntryAsync(string userId, CancellationToken cancellationToken)
    {
        var json = await _cache.GetStringAsync(GetCacheKey(userId, LockoutPrefix), cancellationToken);

        if (json is null)
        {
            return new LockoutEntry();
        }

        return JsonSerializer.Deserialize<LockoutEntry>(json) ?? new LockoutEntry();
    }

    private async Task SaveLockoutEntryAsync(string userId, LockoutEntry info, CancellationToken cancellationToken)
    {
        var key = GetCacheKey(userId, LockoutPrefix);

        if (info.AccessFailedCount == 0 && info.LockoutEndDate is null)
        {
            await _cache.RemoveAsync(key, cancellationToken);
            return;
        }

        await _cache.SetStringAsync(key, JsonSerializer.Serialize(info), cancellationToken);
    }
}
