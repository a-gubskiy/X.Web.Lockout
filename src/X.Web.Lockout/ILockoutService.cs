namespace X.Web.Lockout;

public interface ILockoutService
{
    /// <summary>
    /// Checks whether the given userId is currently locked out.
    /// </summary>
    Task<bool> GetLockoutEnabledAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a failed authentication attempt.
    /// </summary>
    Task IncrementAccessFailedCountAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the failed attempt counter on successful authentication.
    /// </summary>
    Task ResetAccessFailedCountAsync(string userId, CancellationToken cancellationToken = default);
}
