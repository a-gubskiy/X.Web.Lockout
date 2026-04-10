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

    public Task RecordAccessFailedAttemptAsync(string userId, CancellationToken cancellationToken = default)
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

        _cache.Set(key, entry);

        return Task.CompletedTask;
    }

    public Task ResetAccessFailedAttemptsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(userId);

        _cache.Remove(key);

        return Task.CompletedTask;
    }

    private static string BuildKey(string userId) => $"{KeyPrefix}:{userId}";
}
