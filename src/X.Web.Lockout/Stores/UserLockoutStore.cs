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

    // Write paths always publish a FRESH LockoutEntry instead of mutating the stored reference,
    // so concurrent unlocked readers (GetLockoutEndDateAsync / GetAccessFailedCountAsync) either
    // see the old reference or the new reference — never a partially-updated struct field.

    public override async Task<DateTimeOffset?> GetLockoutEndDateAsync(TUser user, CancellationToken cancellationToken)
    {
        var userId = await GetUserIdAsync(user, cancellationToken);

        return _lockoutInfos.TryGetValue(userId, out var info) ? info.LockoutEndDate : null;
    }

    public override async Task SetLockoutEndDateAsync(
        TUser user,
        DateTimeOffset? lockoutEnd,
        CancellationToken cancellationToken)
    {
        var userId = await GetUserIdAsync(user, cancellationToken);

        using var lease = await UserOperations.AcquireAsync(userId, cancellationToken);

        var current = _lockoutInfos.TryGetValue(userId, out var existing) ? existing : null;
        var updated = new LockoutEntry
        {
            AccessFailedCount = current?.AccessFailedCount ?? 0,
            LockoutEndDate = lockoutEnd
        };

        SaveLockoutEntry(userId, updated);
    }

    public override async Task<int> IncrementAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
    {
        var userId = await GetUserIdAsync(user, cancellationToken);

        using var lease = await UserOperations.AcquireAsync(userId, cancellationToken);

        var current = _lockoutInfos.TryGetValue(userId, out var existing) ? existing : null;
        var updated = new LockoutEntry
        {
            AccessFailedCount = (current?.AccessFailedCount ?? 0) + 1,
            LockoutEndDate = current?.LockoutEndDate
        };

        SaveLockoutEntry(userId, updated);

        return updated.AccessFailedCount;
    }

    public override async Task ResetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
    {
        var userId = await GetUserIdAsync(user, cancellationToken);

        using var lease = await UserOperations.AcquireAsync(userId, cancellationToken);

        var current = _lockoutInfos.TryGetValue(userId, out var existing) ? existing : null;
        var updated = new LockoutEntry
        {
            AccessFailedCount = 0,
            LockoutEndDate = current?.LockoutEndDate
        };

        SaveLockoutEntry(userId, updated);
    }

    public override async Task<int> GetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
    {
        var userId = await GetUserIdAsync(user, cancellationToken);

        return _lockoutInfos.TryGetValue(userId, out var info) ? info.AccessFailedCount : 0;
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
