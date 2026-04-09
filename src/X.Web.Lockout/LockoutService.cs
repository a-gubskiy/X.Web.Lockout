using System.Collections.Concurrent;
using Microsoft.AspNetCore.Identity;

namespace X.Web.Lockout;

public class LockoutService : ILockoutService
{
    private readonly LockoutOptions _options;
    private readonly ConcurrentDictionary<string, LockoutEntry> _entries = new();

    public LockoutService(LockoutOptions options)
    {
        _options = options;
    }

    public bool GetLockoutEnabled(string identifier)
    {
        if (!_entries.TryGetValue(identifier, out var entry))
        {
            return false;
        }

        return entry.LockoutEnd.HasValue && entry.LockoutEnd.Value > DateTimeOffset.UtcNow;
    }

    public void RecordAccessFailedAttempt(string identifier)
    {
        var entry = _entries.GetOrAdd(identifier, _ => new LockoutEntry());

        entry.FailedAccessCount++;

        if (entry.FailedAccessCount >= _options.MaxFailedAccessAttempts)
        {
            entry.LockoutEnd = DateTimeOffset.UtcNow.Add(_options.DefaultLockoutTimeSpan);
        }
    }

    public void ResetAccessFailedAttempts(string identifier)
    {
        _entries.TryRemove(identifier, out _);
    }
}
