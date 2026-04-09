using System.Collections.Concurrent;
using Microsoft.AspNetCore.Identity;

namespace X.Web.Lockout;

public class LockoutService : ILockoutService
{
    private readonly LockoutOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, LockoutEntry> _entries = new();

    public LockoutService(LockoutOptions options)
        : this(options, TimeProvider.System)
    {
    }

    public LockoutService(LockoutOptions options, TimeProvider timeProvider)
    {
        _options = options;
        _timeProvider = timeProvider;
    }

    public Task<bool> GetLockoutEnabledAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (!_entries.TryGetValue(userId, out var entry))
        {
            return Task.FromResult(false);
        }

        var result = entry.LockoutEnd.HasValue && entry.LockoutEnd.Value > _timeProvider.GetUtcNow();

        return Task.FromResult(result);
    }

    public Task RecordAccessFailedAttemptAsync(string userId, CancellationToken cancellationToken = default)
    {
        var entry = _entries.GetOrAdd(userId, _ => new LockoutEntry());

        entry.FailedAccessCount++;

        if (entry.FailedAccessCount >= _options.MaxFailedAccessAttempts)
        {
            entry.LockoutEnd = _timeProvider.GetUtcNow().Add(_options.DefaultLockoutTimeSpan);
        }

        return Task.CompletedTask;
    }

    public Task ResetAccessFailedAttemptsAsync(string userId, CancellationToken cancellationToken = default)
    {
        _entries.TryRemove(userId, out _);

        return Task.CompletedTask;
    }
}
