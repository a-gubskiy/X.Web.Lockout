namespace X.Web.Lockout;

public interface IUserLockoutService<in TUser> where TUser : class
{
    /// <summary>
    /// Checks whether the given userId is currently locked out.
    /// </summary>
    Task<bool> GetLockoutEnabledAsync(TUser user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a failed authentication attempt.
    /// </summary>
    Task IncrementAccessFailedCountAsync(TUser user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the failed attempt counter on successful authentication.
    /// </summary>
    Task ResetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken = default);
}