using Microsoft.AspNetCore.Identity;
using X.Web.Lockout.Internal;

namespace X.Web.Lockout.Services;

public class StoreUserLockoutService<TUser> : IUserLockoutService<TUser> where TUser : class
{
    private static readonly AsyncKeyedLock UserOperations = new();

    private readonly LockoutOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly IUserLockoutStore<TUser> _store;

    public StoreUserLockoutService(LockoutOptions options, IUserLockoutStore<TUser> store)
        : this(options, TimeProvider.System, store)
    {
    }

    public StoreUserLockoutService(LockoutOptions options, TimeProvider timeProvider, IUserLockoutStore<TUser> store)
    {
        _options = options;
        _timeProvider = timeProvider;
        _store = store;
    }

    public async Task<bool> GetLockoutEnabledAsync(TUser user, CancellationToken cancellationToken = default)
    {
        var lockoutEnd = await _store.GetLockoutEndDateAsync(user, cancellationToken);

        return lockoutEnd.HasValue && lockoutEnd.Value > _timeProvider.GetUtcNow();
    }

    public async Task IncrementAccessFailedCountAsync(TUser user, CancellationToken cancellationToken = default)
    {
        var userId = await _store.GetUserIdAsync(user, cancellationToken);

        using (await UserOperations.AcquireAsync(userId, cancellationToken))
        {
            var count = await _store.IncrementAccessFailedCountAsync(user, cancellationToken);

            if (count >= _options.MaxFailedAccessAttempts)
            {
                var lockoutEnd = _timeProvider.GetUtcNow().Add(_options.DefaultLockoutTimeSpan);

                await _store.SetLockoutEndDateAsync(user, lockoutEnd, cancellationToken);
            }
        }
    }

    public async Task ResetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken = default)
    {
        var userId = await _store.GetUserIdAsync(user, cancellationToken);

        using (await UserOperations.AcquireAsync(userId, cancellationToken))
        {
            await _store.ResetAccessFailedCountAsync(user, cancellationToken);

            await _store.SetLockoutEndDateAsync(user, null, cancellationToken);
        }
    }
}