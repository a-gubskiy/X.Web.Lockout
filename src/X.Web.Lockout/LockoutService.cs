using Microsoft.AspNetCore.Identity;

namespace X.Web.Lockout;

public class LockoutService<TUser> where TUser : class
{
    private readonly LockoutOptions _options;

    public LockoutService(LockoutOptions options, IUserLockoutStore<TUser> store)
    {
        _options = options;
    }
}