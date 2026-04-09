namespace X.Web.Lockout;

public sealed record LockoutEntry
{
    public int FailedAccessCount { get; set; }

    public DateTimeOffset? LockoutEnd { get; set; }
}