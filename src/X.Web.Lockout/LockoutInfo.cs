namespace X.Web.Lockout;

public sealed record LockoutInfo
{
    public int FailedAccessCount { get; set; }

    public DateTimeOffset? LockoutEnd { get; set; }

    public bool LockoutEnabled { get; set; }
}