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

        return entry.LockoutEnd.HasValue && entry.LockoutEnd.Value > _timeProvider.GetUtcNow();
    }

    public async Task RecordAccessFailedAttemptAsync(string userId, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(userId);
        var entry = await GetEntryAsync(key, cancellationToken) ?? new LockoutEntry();

        entry.FailedAccessCount++;

        if (entry.FailedAccessCount >= _options.MaxFailedAccessAttempts)
        {
            entry.LockoutEnd = _timeProvider.GetUtcNow().Add(_options.DefaultLockoutTimeSpan);
        }

        await SaveEntryAsync(key, entry, cancellationToken);
    }

    public async Task ResetAccessFailedAttemptsAsync(string userId, CancellationToken cancellationToken = default)
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

        await _cache.SetStringAsync(key, json, cancellationToken);
    }

    private static string BuildKey(string userId) => $"{KeyPrefix}:{userId}";
}
