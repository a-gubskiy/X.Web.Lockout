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

    public bool IsLockedOut(string identifier, string ip)
    {
        var key = BuildKey(identifier, ip);

        if (!_entries.TryGetValue(key, out var entry))
        {
            return false;
        }

        if (entry.LockoutEnd.HasValue && entry.LockoutEnd.Value > DateTimeOffset.UtcNow)
        {
            return true;
        }

        return false;
    }

    public void RecordFailedAttempt(string identifier, string ip)
    {
        var key = BuildKey(identifier, ip);

        var entry = _entries.GetOrAdd(key, _ => new LockoutEntry());

        entry.FailedAccessCount++;

        if (entry.FailedAccessCount >= _options.MaxFailedAccessAttempts)
        {
            entry.LockoutEnd = DateTimeOffset.UtcNow.Add(_options.DefaultLockoutTimeSpan);
        }
    }

    public void ResetFailedAccessAttempts(string identifier, string ip)
    {
        var key = BuildKey(identifier, ip);

        _entries.TryRemove(key, out _);
    }

    private static string BuildKey(string identifier, string ip) => $"{identifier}:{ip}";
}
