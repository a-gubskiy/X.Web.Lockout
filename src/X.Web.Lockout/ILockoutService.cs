namespace X.Web.Lockout;

public interface ILockoutService
{
    /// <summary>
    /// Checks whether the given identifier+IP combination is currently locked out.
    /// </summary>
    bool IsLockedOut(string identifier, string ip);

    /// <summary>
    /// Records a failed authentication attempt.
    /// </summary>
    void RecordFailedAttempt(string identifier, string ip);

    /// <summary>
    /// Resets the failed attempt counter on successful authentication.
    /// </summary>
    void ResetFailedAccessAttempts(string identifier, string ip);
}
