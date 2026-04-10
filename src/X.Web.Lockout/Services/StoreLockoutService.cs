using Microsoft.AspNetCore.Identity;

namespace X.Web.Lockout.Services;

/// <summary>
/// Adapts <see cref="StoreUserLockoutService{TUser}"/> to the userId-based
/// <see cref="ILockoutService"/> contract by resolving the user via
/// <see cref="IUserLockoutStore{TUser}.FindByIdAsync"/> before each call.
/// </summary>
public class StoreLockoutService<TUser> : ILockoutService where TUser : class
{
    private readonly IUserLockoutStore<TUser> _store;
    private readonly IUserLockoutService<TUser> _inner;

    public StoreLockoutService(LockoutOptions options, IUserLockoutStore<TUser> store)
        : this(options, TimeProvider.System, store)
    {
    }

    public StoreLockoutService(LockoutOptions options, TimeProvider timeProvider, IUserLockoutStore<TUser> store)
    {
        _store = store;
        _inner = new StoreUserLockoutService<TUser>(options, timeProvider, store);
    }

    public async Task<bool> GetLockoutEnabledAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await FindUserOrThrowAsync(userId, cancellationToken);

        return await _inner.GetLockoutEnabledAsync(user, cancellationToken);
    }

    public async Task IncrementAccessFailedCountAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await FindUserOrThrowAsync(userId, cancellationToken);

        await _inner.IncrementAccessFailedCountAsync(user, cancellationToken);
    }

    public async Task ResetAccessFailedCountAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await FindUserOrThrowAsync(userId, cancellationToken);

        await _inner.ResetAccessFailedCountAsync(user, cancellationToken);
    }

    private async Task<TUser> FindUserOrThrowAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _store.FindByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            throw new InvalidOperationException($"User with userId '{userId}' was not found.");
        }

        return user;
    }
}
