using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;

namespace X.Web.Lockout.Services;

public class MemoryLockoutService : ILockoutService
{
    private const string KeyPrefix = "lockout";

    private readonly LockoutOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly IMemoryCache _cache;

    public MemoryLockoutService(LockoutOptions options, IMemoryCache cache)
        : this(options, TimeProvider.System, cache)
    {
    }

    public MemoryLockoutService(LockoutOptions options, TimeProvider timeProvider, IMemoryCache cache)
    {
        _options = options;
        _timeProvider = timeProvider;
        _cache = cache;
    }

    public Task<bool> GetLockoutEnabledAsync(string userId, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(userId);

        if (!_cache.TryGetValue<LockoutEntry>(key, out var entry) || entry is null)
        {
            return Task.FromResult(false);
        }

        var result = entry.LockoutEnd.HasValue && entry.LockoutEnd.Value > _timeProvider.GetUtcNow();

        return Task.FromResult(result);
    }

    public Task IncrementAccessFailedCountAsync(string userId, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(userId);

        if (!_cache.TryGetValue<LockoutEntry>(key, out var entry) || entry is null)
        {
            entry = new LockoutEntry();
        }

        entry.FailedAccessCount++;

        if (entry.FailedAccessCount >= _options.MaxFailedAccessAttempts)
        {
            entry.LockoutEnd = _timeProvider.GetUtcNow().Add(_options.DefaultLockoutTimeSpan);
        }

        _cache.Set(key, entry, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = GetEntryLifetime(entry)
        });

        return Task.CompletedTask;
    }

    public Task ResetAccessFailedCountAsync(string userId, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(userId);

        _cache.Remove(key);

        return Task.CompletedTask;
    }

    private static string BuildKey(string userId) => $"{KeyPrefix}:{userId}";

    private TimeSpan GetEntryLifetime(LockoutEntry entry)
    {
        // If locked out, live until the lockout ends so the entry self-evicts.
        // Otherwise, use DefaultLockoutTimeSpan as a sliding window for tracking
        // failed attempts — an attacker who pauses longer than that gets a clean slate,
        // matching standard brute-force throttling behavior.
        if (entry.LockoutEnd.HasValue)
        {
            var remaining = entry.LockoutEnd.Value - _timeProvider.GetUtcNow();

            if (remaining > TimeSpan.Zero)
            {
                return remaining;
            }
        }

        return _options.DefaultLockoutTimeSpan;
    }
}
