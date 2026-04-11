using System.Collections.Concurrent;
using Microsoft.AspNetCore.Identity;
using X.Web.Lockout.Internal;

namespace X.Web.Lockout.Stores;

public class UserLockoutStore<TUser> : IUserLockoutStore<TUser> where TUser : class
{
    private static readonly AsyncKeyedLock UserOperations = new();

    private readonly IUserStore<TUser> _userStore;
    private readonly ConcurrentDictionary<string, LockoutEntry> _lockoutInfos = new();
    private readonly ConcurrentDictionary<string, bool> _lockoutEnabled = new();

    public UserLockoutStore(IUserStore<TUser> userStore)
    {
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
        var info = GetLockoutEntry(userId);

        return info.LockoutEndDate;
    }

    public async Task SetLockoutEndDateAsync(
        TUser user,
        DateTimeOffset? lockoutEnd,
        CancellationToken cancellationToken)
    {
        var userId = await _userStore.GetUserIdAsync(user, cancellationToken);

        using var lease = await UserOperations.AcquireAsync(userId, cancellationToken);

        var info = GetLockoutEntry(userId);

        info.LockoutEndDate = lockoutEnd;

        SaveLockoutEntry(userId, info);
    }

    public async Task<int> IncrementAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
    {
        var userId = await _userStore.GetUserIdAsync(user, cancellationToken);

        using var lease = await UserOperations.AcquireAsync(userId, cancellationToken);

        var info = GetLockoutEntry(userId);
        info.AccessFailedCount++;

        SaveLockoutEntry(userId, info);

        return info.AccessFailedCount;
    }

    public async Task ResetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
    {
        var userId = await _userStore.GetUserIdAsync(user, cancellationToken);

        using var lease = await UserOperations.AcquireAsync(userId, cancellationToken);

        var info = GetLockoutEntry(userId);
        info.AccessFailedCount = 0;

        SaveLockoutEntry(userId, info);
    }

    public async Task<int> GetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
    {
        var userId = await _userStore.GetUserIdAsync(user, cancellationToken);
        var info = GetLockoutEntry(userId);

        return info.AccessFailedCount;
    }

    public async Task<bool> GetLockoutEnabledAsync(TUser user, CancellationToken cancellationToken)
    {
        var userId = await _userStore.GetUserIdAsync(user, cancellationToken);

        return _lockoutEnabled.TryGetValue(userId, out var enabled) && enabled;
    }

    public async Task SetLockoutEnabledAsync(TUser user, bool enabled, CancellationToken cancellationToken)
    {
        var userId = await _userStore.GetUserIdAsync(user, cancellationToken);

        using var lease = await UserOperations.AcquireAsync(userId, cancellationToken);

        _lockoutEnabled[userId] = enabled;
    }

    private LockoutEntry GetLockoutEntry(string userId)
    {
        if (_lockoutInfos.TryGetValue(userId, out var info))
        {
            return info;
        }

        return new LockoutEntry();
    }

    private void SaveLockoutEntry(string userId, LockoutEntry info)
    {
        _lockoutInfos[userId] = info;
    }
}
