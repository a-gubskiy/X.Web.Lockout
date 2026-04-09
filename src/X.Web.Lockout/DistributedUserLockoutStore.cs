using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;

namespace X.Web.Lockout;

public class DistributedUserLockoutStore<TUser> : IUserLockoutStore<TUser> where TUser : class
{
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
        var info = await GetLockoutInfoAsync(user, cancellationToken);

        return info.LockoutEnd;
    }

    public async Task SetLockoutEndDateAsync(
        TUser user,
        DateTimeOffset? lockoutEnd,
        CancellationToken cancellationToken)
    {
        var info = await GetLockoutInfoAsync(user, cancellationToken);

        info.LockoutEnd = lockoutEnd;

        await SaveLockoutInfoAsync(user, info, cancellationToken);
    }

    public async Task<int> IncrementAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
    {
        var info = await GetLockoutInfoAsync(user, cancellationToken);
        info.FailedAccessCount++;

        await SaveLockoutInfoAsync(user, info, cancellationToken);

        return info.FailedAccessCount;
    }

    public async Task ResetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
    {
        var info = await GetLockoutInfoAsync(user, cancellationToken);
        info.FailedAccessCount = 0;

        await SaveLockoutInfoAsync(user, info, cancellationToken);
    }

    public async Task<int> GetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
    {
        var info = await GetLockoutInfoAsync(user, cancellationToken);

        return info.FailedAccessCount;
    }

    public async Task<bool> GetLockoutEnabledAsync(TUser user, CancellationToken cancellationToken)
    {
        var info = await GetLockoutInfoAsync(user, cancellationToken);

        return info.LockoutEnabled;
    }

    public async Task SetLockoutEnabledAsync(TUser user, bool enabled, CancellationToken cancellationToken)
    {
        var info = await GetLockoutInfoAsync(user, cancellationToken);

        info.LockoutEnabled = enabled;

        await SaveLockoutInfoAsync(user, info, cancellationToken);
    }

    private async Task<string> GetCacheKeyAsync(TUser user, CancellationToken cancellationToken)
    {
        var userId = await _userStore.GetUserIdAsync(user, cancellationToken);

        return $"lockout:{userId}";
    }

    private async Task<LockoutInfo> GetLockoutInfoAsync(TUser user, CancellationToken cancellationToken)
    {
        var key = await GetCacheKeyAsync(user, cancellationToken);
        var json = await _cache.GetStringAsync(key, cancellationToken);

        if (json is null)
        {
            return new LockoutInfo();
        }

        return JsonSerializer.Deserialize<LockoutInfo>(json) ?? new LockoutInfo();
    }

    private async Task SaveLockoutInfoAsync(TUser user, LockoutInfo info, CancellationToken cancellationToken)
    {
        var key = await GetCacheKeyAsync(user, cancellationToken);
        var json = JsonSerializer.Serialize(info);

        await _cache.SetStringAsync(key, json, cancellationToken);
    }
}