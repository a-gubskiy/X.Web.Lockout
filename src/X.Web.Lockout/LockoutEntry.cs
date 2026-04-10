namespace X.Web.Lockout;

public sealed record LockoutEntry
{
    public int AccessFailedCount { get; set; }

    public DateTimeOffset? LockoutEndDate { get; set; }
}