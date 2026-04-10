using System.Collections.Concurrent;
using Microsoft.AspNetCore.Identity;

namespace X.Web.Lockout.Services;

public class LockoutService : LockoutEntryServiceBase
{
    private readonly ConcurrentDictionary<string, LockoutEntry> _entries = new();

    public LockoutService(LockoutOptions options)
        : this(options, TimeProvider.System)
    {
    }

    public LockoutService(LockoutOptions options, TimeProvider timeProvider)
        : base(options, timeProvider)
    {
    }

    protected override Task<LockoutEntry?> LoadAsync(string userId, CancellationToken cancellationToken)
    {
        _entries.TryGetValue(userId, out var entry);

        return Task.FromResult(entry);
    }

    protected override Task SaveAsync(string userId, LockoutEntry entry, CancellationToken cancellationToken)
    {
        _entries[userId] = entry;

        return Task.CompletedTask;
    }

    protected override Task RemoveAsync(string userId, CancellationToken cancellationToken)
    {
        _entries.TryRemove(userId, out _);

        return Task.CompletedTask;
    }
}
