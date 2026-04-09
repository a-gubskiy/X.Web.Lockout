using Microsoft.AspNetCore.Identity;

namespace X.Web.Lockout;

public class StoreLockoutService<TUser> : ILockoutService where TUser : class
{
    private readonly LockoutOptions _options;
    private readonly IUserLockoutStore<TUser> _store;

    public StoreLockoutService(LockoutOptions options, IUserLockoutStore<TUser> store)
    {
        _options = options;
        _store = store;
    }

    public async Task<bool> GetLockoutEnabledAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await FindUserOrThrowAsync(userId, cancellationToken);
        var lockoutEnd = await _store.GetLockoutEndDateAsync(user, cancellationToken);

        return lockoutEnd.HasValue && lockoutEnd.Value > DateTimeOffset.UtcNow;
    }

    public async Task RecordAccessFailedAttemptAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await FindUserOrThrowAsync(userId, cancellationToken);
        var count = await _store.IncrementAccessFailedCountAsync(user, cancellationToken);

        if (count >= _options.MaxFailedAccessAttempts)
        {
            var lockoutEnd = DateTimeOffset.UtcNow.Add(_options.DefaultLockoutTimeSpan);

            await _store.SetLockoutEndDateAsync(user, lockoutEnd, cancellationToken);
        }
    }

    public async Task ResetAccessFailedAttemptsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await FindUserOrThrowAsync(userId, cancellationToken);

        await _store.ResetAccessFailedCountAsync(user, cancellationToken);
        await _store.SetLockoutEndDateAsync(user, null, cancellationToken);
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
