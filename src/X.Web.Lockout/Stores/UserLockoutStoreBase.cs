using Microsoft.AspNetCore.Identity;

namespace X.Web.Lockout.Stores;

/// <summary>
/// Base class for <see cref="IUserLockoutStore{TUser}"/> decorators that forward
/// all non-lockout <see cref="IUserStore{TUser}"/> operations to an inner store.
/// Derived classes only need to implement the lockout-specific members.
/// </summary>
public abstract class UserLockoutStoreBase<TUser> : IUserLockoutStore<TUser> where TUser : class
{
    private readonly IUserStore<TUser> _inner;

    protected UserLockoutStoreBase(IUserStore<TUser> inner)
    {
        _inner = inner;
    }

    public virtual void Dispose()
    {
    }

    public Task<string> GetUserIdAsync(TUser user, CancellationToken cancellationToken) =>
        _inner.GetUserIdAsync(user, cancellationToken);

    public Task<string?> GetUserNameAsync(TUser user, CancellationToken cancellationToken) =>
        _inner.GetUserNameAsync(user, cancellationToken);

    public Task SetUserNameAsync(TUser user, string? userName, CancellationToken cancellationToken) =>
        _inner.SetUserNameAsync(user, userName, cancellationToken);

    public Task<string?> GetNormalizedUserNameAsync(TUser user, CancellationToken cancellationToken) =>
        _inner.GetNormalizedUserNameAsync(user, cancellationToken);

    public Task SetNormalizedUserNameAsync(TUser user, string? normalizedName, CancellationToken cancellationToken) =>
        _inner.SetNormalizedUserNameAsync(user, normalizedName, cancellationToken);

    public Task<IdentityResult> CreateAsync(TUser user, CancellationToken cancellationToken) =>
        _inner.CreateAsync(user, cancellationToken);

    public Task<IdentityResult> UpdateAsync(TUser user, CancellationToken cancellationToken) =>
        _inner.UpdateAsync(user, cancellationToken);

    public Task<IdentityResult> DeleteAsync(TUser user, CancellationToken cancellationToken) =>
        _inner.DeleteAsync(user, cancellationToken);

    public Task<TUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
        _inner.FindByIdAsync(userId, cancellationToken);

    public Task<TUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
        _inner.FindByNameAsync(normalizedUserName, cancellationToken);

    public abstract Task<DateTimeOffset?> GetLockoutEndDateAsync(TUser user, CancellationToken cancellationToken);

    public abstract Task SetLockoutEndDateAsync(TUser user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken);

    public abstract Task<int> IncrementAccessFailedCountAsync(TUser user, CancellationToken cancellationToken);

    public abstract Task ResetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken);

    public abstract Task<int> GetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken);

    public abstract Task<bool> GetLockoutEnabledAsync(TUser user, CancellationToken cancellationToken);

    public abstract Task SetLockoutEnabledAsync(TUser user, bool enabled, CancellationToken cancellationToken);
}
