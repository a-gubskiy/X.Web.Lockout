using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;

namespace X.Web.Lockout.Services;

public class DistributedLockoutService : ILockoutService
{
    private const string KeyPrefix = "lockout";

    private readonly LockoutOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly IDistributedCache _cache;

    public DistributedLockoutService(LockoutOptions options, IDistributedCache cache)
        : this(options, TimeProvider.System, cache)
    {
    }

    public DistributedLockoutService(LockoutOptions options, TimeProvider timeProvider, IDistributedCache cache)
    {
        _options = options;
        _timeProvider = timeProvider;
        _cache = cache;
    }

    public async Task<bool> GetLockoutEnabledAsync(string userId, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(userId);
        var entry = await GetEntryAsync(key, cancellationToken);

        if (entry is null)
        {
            return false;
        }

        return entry.LockoutEndDate.HasValue && entry.LockoutEndDate.Value > _timeProvider.GetUtcNow();
    }

    public async Task IncrementAccessFailedCountAsync(string userId, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(userId);
        var entry = await GetEntryAsync(key, cancellationToken) ?? new LockoutEntry();

        entry.AccessFailedCount++;

        if (entry.AccessFailedCount >= _options.MaxFailedAccessAttempts)
        {
            entry.LockoutEndDate = _timeProvider.GetUtcNow().Add(_options.DefaultLockoutTimeSpan);
        }

        await SaveEntryAsync(key, entry, cancellationToken);
    }

    public async Task ResetAccessFailedCountAsync(string userId, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(userId);

        await _cache.RemoveAsync(key, cancellationToken);
    }

    private async Task<LockoutEntry?> GetEntryAsync(string key, CancellationToken cancellationToken)
    {
        var json = await _cache.GetStringAsync(key, cancellationToken);

        if (json is null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<LockoutEntry>(json);
    }

    private async Task SaveEntryAsync(string key, LockoutEntry entry, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(entry);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = GetEntryLifetime(entry)
        };

        await _cache.SetStringAsync(key, json, options, cancellationToken);
    }

    private TimeSpan GetEntryLifetime(LockoutEntry entry)
    {
        // If locked out, live until the lockout ends so the entry self-evicts.
        // Otherwise, use DefaultLockoutTimeSpan as a sliding window for tracking
        // failed attempts — an attacker who pauses longer than that gets a clean slate,
        // matching standard brute-force throttling behavior.
        if (entry.LockoutEndDate.HasValue)
        {
            var remaining = entry.LockoutEndDate.Value - _timeProvider.GetUtcNow();

            if (remaining > TimeSpan.Zero)
            {
                return remaining;
            }
        }

        return _options.DefaultLockoutTimeSpan;
    }

    private static string BuildKey(string userId) => $"{KeyPrefix}:{userId}";
}
