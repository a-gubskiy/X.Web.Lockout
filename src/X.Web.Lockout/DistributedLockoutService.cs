using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;

namespace X.Web.Lockout;

public class DistributedLockoutService : ILockoutService
{
    private const string KeyPrefix = "lockout";

    private readonly LockoutOptions _options;
    private readonly IDistributedCache _cache;

    public DistributedLockoutService(LockoutOptions options, IDistributedCache cache)
    {
        _options = options;
        _cache = cache;
    }

    public bool GetLockoutEnabled(string identifier, string ip)
    {
        var key = BuildKey(identifier, ip);
        var entry = GetEntry(key);

        if (entry is null)
        {
            return false;
        }

        return entry.LockoutEnd.HasValue && entry.LockoutEnd.Value > DateTimeOffset.UtcNow;
    }

    public void RecordAccessFailedAttempt(string identifier, string ip)
    {
        var key = BuildKey(identifier, ip);
        var entry = GetEntry(key) ?? new LockoutEntry();

        entry.FailedAccessCount++;

        if (entry.FailedAccessCount >= _options.MaxFailedAccessAttempts)
        {
            entry.LockoutEnd = DateTimeOffset.UtcNow.Add(_options.DefaultLockoutTimeSpan);
        }

        SaveEntry(key, entry);
    }

    public void ResetAccessFailedAttempts(string identifier, string ip)
    {
        var key = BuildKey(identifier, ip);

        _cache.Remove(key);
    }

    private LockoutEntry? GetEntry(string key)
    {
        var json = _cache.GetString(key);

        if (json is null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<LockoutEntry>(json);
    }

    private void SaveEntry(string key, LockoutEntry entry)
    {
        var json = JsonSerializer.Serialize(entry);

        _cache.SetString(key, json);
    }

    private static string BuildKey(string identifier, string ip) => $"{KeyPrefix}:{identifier}:{ip}";
}
