using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using X.Web.Lockout.Internal;

namespace X.Web.Lockout.Stores;

public class DistributedUserLockoutStore<TUser> : IUserLockoutStore<TUser> where TUser : class
{
    private const string LockoutPrefix = "lockout";
    private const string LockoutEnabledPrefix = "lockout-enabled";
    private static readonly AsyncKeyedLock UserOperations = new();

    private readonly IDistributedCache _cache;
    private readonly IUserStore<TUser> _userStore;

    public DistributedUserLockoutStore(IDistributedCache cache, IUserStore<TUser> userStore)
    {
        _cache = cache;
        _userStore = userStore;
    }

    public void Dispose()
    {
    }

    public Task<string> GetUserIdAsync(TUser user, CancellationToken cancellationToken) =>
        _userStore.GetUserIdAsync(user, cancellationToken);

    public Task<string?> GetUserNameAsync(TUser user, CancellationToken cancellationToken) =>
        _userStore.GetUserNameAsync(user, cancellationToken);

    public Task SetUserNameAsync(TUser user, string? userName, CancellationToken cancellationToken) =>
        _userStore.SetUserNameAsync(user, userName, cancellationToken);

    public Task<string?> GetNormalizedUserNameAsync(TUser user, CancellationToken cancellationToken) =>
        _userStore.GetNormalizedUserNameAsync(user, cancellationToken);

    public Task SetNormalizedUserNameAsync(TUser user, string? normalizedName, CancellationToken cancellationToken) =>
        _userStore.SetNormalizedUserNameAsync(user, normalizedName, cancellationToken);

    public Task<IdentityResult> CreateAsync(TUser user, CancellationToken cancellationToken) =>
        _userStore.CreateAsync(user, cancellationToken);

    public Task<IdentityResult> UpdateAsync(TUser user, CancellationToken cancellationToken) =>
        _userStore.UpdateAsync(user, cancellationToken);

    public Task<IdentityResult> DeleteAsync(TUser user, CancellationToken cancellationToken) =>
        _userStore.DeleteAsync(user, cancellationToken);

    public Task<TUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
        _userStore.FindByIdAsync(userId, cancellationToken);

    public Task<TUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
        _userStore.FindByNameAsync(normalizedUserName, cancellationToken);

    public async Task<DateTimeOffset?> GetLockoutEndDateAsync(TUser user, CancellationToken cancellationToken)
    {
        var userId = await _userStore.GetUserIdAsync(user, cancellationToken);
        var info = await GetLockoutEntryAsync(userId, cancellationToken);

        return info.LockoutEndDate;
    }

    public async Task SetLockoutEndDateAsync(
        TUser user,
        DateTimeOffset? lockoutEnd,
        CancellationToken cancellationToken)
    {
        var userId = await _userStore.GetUserIdAsync(user, cancellationToken);

        using (await UserOperations.AcquireAsync(userId, cancellationToken))
        {
            var info = await GetLockoutEntryAsync(userId, cancellationToken);

            info.LockoutEndDate = lockoutEnd;

            await SaveLockoutEntryAsync(userId, info, cancellationToken);
        }
    }

    public async Task<int> IncrementAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
    {
        var userId = await _userStore.GetUserIdAsync(user, cancellationToken);

        using (await UserOperations.AcquireAsync(userId, cancellationToken))
        {
            var info = await GetLockoutEntryAsync(userId, cancellationToken);
            info.AccessFailedCount++;

            await SaveLockoutEntryAsync(userId, info, cancellationToken);

            return info.AccessFailedCount;
        }
    }

    public async Task ResetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
    {
        var userId = await _userStore.GetUserIdAsync(user, cancellationToken);

        using (await UserOperations.AcquireAsync(userId, cancellationToken))
        {
            var info = await GetLockoutEntryAsync(userId, cancellationToken);
            info.AccessFailedCount = 0;

            await SaveLockoutEntryAsync(userId, info, cancellationToken);
        }
    }

    public async Task<int> GetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
    {
        var userId = await _userStore.GetUserIdAsync(user, cancellationToken);
        var info = await GetLockoutEntryAsync(userId, cancellationToken);

        return info.AccessFailedCount;
    }

    public async Task<bool> GetLockoutEnabledAsync(TUser user, CancellationToken cancellationToken)
    {
        var userId = await _userStore.GetUserIdAsync(user, cancellationToken);
        var key = GetCacheKey(userId, LockoutEnabledPrefix);
        var value = await _cache.GetStringAsync(key, cancellationToken);

        return value is not null && bool.Parse(value);
    }

    public async Task SetLockoutEnabledAsync(TUser user, bool enabled, CancellationToken cancellationToken)
    {
        var userId = await _userStore.GetUserIdAsync(user, cancellationToken);

        using (await UserOperations.AcquireAsync(userId, cancellationToken))
        {
            var key = GetCacheKey(userId, LockoutEnabledPrefix);

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
        var key = GetCacheKey(userId, LockoutPrefix);
        var json = await _cache.GetStringAsync(key, cancellationToken);

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

        var json = JsonSerializer.Serialize(info);

        await _cache.SetStringAsync(key, json, cancellationToken);
    }
}
