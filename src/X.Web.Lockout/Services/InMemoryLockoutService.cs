using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;

namespace X.Web.Lockout.Services;

public class MemoryLockoutService : LockoutServiceBase
{
    private const string KeyPrefix = "lockout";

    private readonly IMemoryCache _cache;

    public MemoryLockoutService(LockoutOptions options, IMemoryCache cache)
        : this(options, TimeProvider.System, cache)
    {
    }

    public MemoryLockoutService(LockoutOptions options, TimeProvider timeProvider, IMemoryCache cache)
        : base(options, timeProvider)
    {
        _cache = cache;
    }

    protected override Task<LockoutEntry?> LoadAsync(string userId, CancellationToken cancellationToken)
    {
        _cache.TryGetValue<LockoutEntry>(BuildKey(userId), out var entry);

        return Task.FromResult(entry);
    }

    protected override Task SaveAsync(string userId, LockoutEntry entry, CancellationToken cancellationToken)
    {
        _cache.Set(BuildKey(userId), entry, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = GetEntryLifetime(entry)
        });

        return Task.CompletedTask;
    }

    protected override Task RemoveAsync(string userId, CancellationToken cancellationToken)
    {
        _cache.Remove(BuildKey(userId));

        return Task.CompletedTask;
    }

    private static string BuildKey(string userId) => $"{KeyPrefix}:{userId}";
}
