using Microsoft.AspNetCore.Identity;
using X.Web.Lockout.Internal;

namespace X.Web.Lockout.Services;

/// <summary>
/// Base class for <see cref="ILockoutService"/> implementations that persist state
/// as a single <see cref="LockoutEntry"/> blob (in-memory dictionary, <c>IMemoryCache</c>,
/// <c>IDistributedCache</c>, etc.). Derived classes only need to implement load, save,
/// and remove operations; the threshold logic and time checks live here.
/// </summary>
public abstract class LockoutServiceBase : ILockoutService
{
    private static readonly AsyncKeyedLock UserOperations = new();

    protected LockoutOptions Options { get; }
    protected TimeProvider TimeProvider { get; }

    protected LockoutServiceBase(LockoutOptions options, TimeProvider timeProvider)
    {
        Options = options;
        TimeProvider = timeProvider;
    }

    public async Task<bool> GetLockoutEnabledAsync(string userId, CancellationToken cancellationToken = default)
    {
        var entry = await LoadAsync(userId, cancellationToken);

        if (entry is null)
        {
            return false;
        }

        return entry.LockoutEndDate.HasValue && entry.LockoutEndDate.Value > TimeProvider.GetUtcNow();
    }

    public async Task IncrementAccessFailedCountAsync(string userId, CancellationToken cancellationToken = default)
    {
        using var lease = await UserOperations.AcquireAsync(userId, cancellationToken);

        var entry = await LoadAsync(userId, cancellationToken) ?? new LockoutEntry();

        entry.AccessFailedCount++;

        if (entry.AccessFailedCount >= Options.MaxFailedAccessAttempts)
        {
            entry.LockoutEndDate = TimeProvider.GetUtcNow().Add(Options.DefaultLockoutTimeSpan);
        }

        await SaveAsync(userId, entry, cancellationToken);
    }

    public async Task ResetAccessFailedCountAsync(string userId, CancellationToken cancellationToken = default)
    {
        using (await UserOperations.AcquireAsync(userId, cancellationToken))
        {
            await RemoveAsync(userId, cancellationToken);
        }
    }

    /// <summary>
    /// Loads the <see cref="LockoutEntry"/> for the given user, or <c>null</c> if there is none.
    /// </summary>
    protected abstract Task<LockoutEntry?> LoadAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Persists the <see cref="LockoutEntry"/> for the given user.
    /// </summary>
    protected abstract Task SaveAsync(string userId, LockoutEntry entry, CancellationToken cancellationToken);

    /// <summary>
    /// Removes any stored lockout state for the given user.
    /// </summary>
    protected abstract Task RemoveAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Calculates the lifetime for a cached lockout entry.
    /// If locked out, returns the remaining lockout time so the entry self-evicts.
    /// Otherwise, returns <see cref="LockoutOptions.DefaultLockoutTimeSpan"/> as a sliding window
    /// for tracking failed attempts — an attacker who pauses longer gets a clean slate.
    /// </summary>
    protected TimeSpan GetEntryLifetime(LockoutEntry entry)
    {
        if (entry.LockoutEndDate.HasValue)
        {
            var remaining = entry.LockoutEndDate.Value - TimeProvider.GetUtcNow();

            if (remaining > TimeSpan.Zero)
            {
                return remaining;
            }
        }

        return Options.DefaultLockoutTimeSpan;
    }
}
