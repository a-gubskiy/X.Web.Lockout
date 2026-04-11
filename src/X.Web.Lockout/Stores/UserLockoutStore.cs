using System.Collections.Concurrent;
using Microsoft.AspNetCore.Identity;
using X.Web.Lockout.Internal;

namespace X.Web.Lockout.Stores;

public class UserLockoutStore<TUser> : UserLockoutStoreBase<TUser> where TUser : class
{
    private static readonly AsyncKeyedLock UserOperations = new();

    private readonly ConcurrentDictionary<string, LockoutEntry> _lockoutInfos = new();
    private readonly ConcurrentDictionary<string, bool> _lockoutEnabled = new();

    public UserLockoutStore(IUserStore<TUser> userStore) : base(userStore)
    {
    }

    public override async Task<DateTimeOffset?> GetLockoutEndDateAsync(TUser user, CancellationToken cancellationToken)
    {
        var userId = await GetUserIdAsync(user, cancellationToken);

        return GetLockoutEntry(userId).LockoutEndDate;
    }

    public override async Task SetLockoutEndDateAsync(
        TUser user,
        DateTimeOffset? lockoutEnd,
        CancellationToken cancellationToken)
    {
        var userId = await GetUserIdAsync(user, cancellationToken);

        using var lease = await UserOperations.AcquireAsync(userId, cancellationToken);

        var info = GetLockoutEntry(userId);
        info.LockoutEndDate = lockoutEnd;

        SaveLockoutEntry(userId, info);
    }

    public override async Task<int> IncrementAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
    {
        var userId = await GetUserIdAsync(user, cancellationToken);

        using var lease = await UserOperations.AcquireAsync(userId, cancellationToken);

        var info = GetLockoutEntry(userId);
        info.AccessFailedCount++;

        SaveLockoutEntry(userId, info);

        return info.AccessFailedCount;
    }

    public override async Task ResetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
    {
        var userId = await GetUserIdAsync(user, cancellationToken);

        using var lease = await UserOperations.AcquireAsync(userId, cancellationToken);

        var info = GetLockoutEntry(userId);
        info.AccessFailedCount = 0;

        SaveLockoutEntry(userId, info);
    }

    public override async Task<int> GetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
    {
        var userId = await GetUserIdAsync(user, cancellationToken);

        return GetLockoutEntry(userId).AccessFailedCount;
    }

    public override async Task<bool> GetLockoutEnabledAsync(TUser user, CancellationToken cancellationToken)
    {
        var userId = await GetUserIdAsync(user, cancellationToken);

        return _lockoutEnabled.ContainsKey(userId);
    }

    public override async Task SetLockoutEnabledAsync(TUser user, bool enabled, CancellationToken cancellationToken)
    {
        var userId = await GetUserIdAsync(user, cancellationToken);

        using var lease = await UserOperations.AcquireAsync(userId, cancellationToken);

        if (enabled)
        {
            _lockoutEnabled[userId] = true;
            return;
        }

        _lockoutEnabled.TryRemove(userId, out _);
    }

    private LockoutEntry GetLockoutEntry(string userId) =>
        _lockoutInfos.TryGetValue(userId, out var info) ? info : new LockoutEntry();

    private void SaveLockoutEntry(string userId, LockoutEntry info)
    {
        if (info.AccessFailedCount == 0 && info.LockoutEndDate is null)
        {
            _lockoutInfos.TryRemove(userId, out _);
            return;
        }

        _lockoutInfos[userId] = info;
    }
}
