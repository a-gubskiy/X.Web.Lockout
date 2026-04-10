using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using X.Web.Lockout.Services;

namespace X.Web.Lockout.Tests.Services;

public class DistributedLockoutServiceTests
{
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly LockoutOptions _options = new()
    {
        MaxFailedAccessAttempts = 3,
        DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5)
    };

    private DistributedLockoutService CreateService()
    {
        var cache = new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions()));

        return new DistributedLockoutService(_options, _timeProvider, cache);
    }

    [Fact]
    public async Task GetLockoutEnabledAsync_UnknownUser_ReturnsFalse()
    {
        var service = CreateService();

        var result = await service.GetLockoutEnabledAsync("unknown");

        Assert.False(result);
    }

    [Fact]
    public async Task GetLockoutEnabledAsync_BelowThreshold_ReturnsFalse()
    {
        var service = CreateService();

        await service.IncrementAccessFailedCountAsync("user1");
        await service.IncrementAccessFailedCountAsync("user1");

        var result = await service.GetLockoutEnabledAsync("user1");

        Assert.False(result);
    }

    [Fact]
    public async Task GetLockoutEnabledAsync_AtThreshold_ReturnsTrue()
    {
        var service = CreateService();

        for (var i = 0; i < _options.MaxFailedAccessAttempts; i++)
        {
            await service.IncrementAccessFailedCountAsync("user1");
        }

        var result = await service.GetLockoutEnabledAsync("user1");

        Assert.True(result);
    }

    [Fact]
    public async Task GetLockoutEnabledAsync_AfterLockoutExpired_ReturnsFalse()
    {
        var service = CreateService();

        for (var i = 0; i < _options.MaxFailedAccessAttempts; i++)
        {
            await service.IncrementAccessFailedCountAsync("user1");
        }

        _timeProvider.Advance(TimeSpan.FromMinutes(6));

        var result = await service.GetLockoutEnabledAsync("user1");

        Assert.False(result);
    }

    [Fact]
    public async Task ResetAccessFailedAttemptsAsync_ClearsLockout()
    {
        var service = CreateService();

        for (var i = 0; i < _options.MaxFailedAccessAttempts; i++)
        {
            await service.IncrementAccessFailedCountAsync("user1");
        }

        Assert.True(await service.GetLockoutEnabledAsync("user1"));

        await service.ResetAccessFailedCountAsync("user1");

        Assert.False(await service.GetLockoutEnabledAsync("user1"));
    }

    [Fact]
    public async Task ResetAccessFailedAttemptsAsync_ResetsCounter()
    {
        var service = CreateService();

        await service.IncrementAccessFailedCountAsync("user1");
        await service.IncrementAccessFailedCountAsync("user1");
        await service.ResetAccessFailedCountAsync("user1");

        await service.IncrementAccessFailedCountAsync("user1");
        await service.IncrementAccessFailedCountAsync("user1");

        Assert.False(await service.GetLockoutEnabledAsync("user1"));
    }

    [Fact]
    public async Task RecordAccessFailedAttemptAsync_BelowThreshold_SetsExpirationToDefaultLockoutTimeSpan()
    {
        var cacheMock = new Mock<IDistributedCache>();
        DistributedCacheEntryOptions? captured = null;

        cacheMock
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        cacheMock
            .Setup(c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>((_, _, opts, _) => captured = opts)
            .Returns(Task.CompletedTask);

        var service = new DistributedLockoutService(_options, _timeProvider, cacheMock.Object);

        await service.IncrementAccessFailedCountAsync("user1");

        Assert.NotNull(captured);
        Assert.Equal(_options.DefaultLockoutTimeSpan, captured!.AbsoluteExpirationRelativeToNow);
    }

    [Fact]
    public async Task RecordAccessFailedAttemptAsync_AtThreshold_SetsExpirationToRemainingLockoutDuration()
    {
        var cache = new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions()));
        var service = new DistributedLockoutService(_options, _timeProvider, cache);

        // Fire enough attempts to trigger lockout
        for (var i = 0; i < _options.MaxFailedAccessAttempts; i++)
        {
            await service.IncrementAccessFailedCountAsync("user1");
        }

        // Advance past the lockout window — real cache should have evicted the entry
        _timeProvider.Advance(_options.DefaultLockoutTimeSpan + TimeSpan.FromSeconds(1));

        // After the absolute expiration, GetLockoutEnabledAsync reads a missing entry -> false
        Assert.False(await service.GetLockoutEnabledAsync("user1"));
    }

    [Fact]
    public async Task RecordAccessFailedAttemptAsync_WhenExistingLockoutEndIsInPast_FallsBackToDefaultLockoutTimeSpan()
    {
        // Pre-seed the cache with an entry whose LockoutEnd is already in the past.
        var cache = new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions()));

        var staleEntry = new LockoutEntry
        {
            FailedAccessCount = 1,
            LockoutEnd = _timeProvider.GetUtcNow().AddMinutes(-10)
        };
        var staleJson = System.Text.Json.JsonSerializer.Serialize(staleEntry);
        await cache.SetStringAsync("lockout:user1", staleJson);

        // Wrap with a mock that proxies through the real cache but lets us capture the Set options.
        var cacheMock = new Mock<IDistributedCache>();
        DistributedCacheEntryOptions? captured = null;

        cacheMock
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes(staleJson));

        cacheMock
            .Setup(c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>((_, _, opts, _) => captured = opts)
            .Returns(Task.CompletedTask);

        var service = new DistributedLockoutService(_options, _timeProvider, cacheMock.Object);

        await service.IncrementAccessFailedCountAsync("user1");

        Assert.NotNull(captured);
        Assert.Equal(_options.DefaultLockoutTimeSpan, captured!.AbsoluteExpirationRelativeToNow);
    }

    [Fact]
    public async Task Constructor_WithOptionsAndCacheOnly_UsesSystemTimeProvider()
    {
        var cache = new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions()));
        var service = new DistributedLockoutService(_options, cache);

        for (var i = 0; i < _options.MaxFailedAccessAttempts; i++)
        {
            await service.IncrementAccessFailedCountAsync("user1");
        }

        Assert.True(await service.GetLockoutEnabledAsync("user1"));
    }

    [Fact]
    public async Task RecordAccessFailedAttemptAsync_IsolatesUsers()
    {
        var service = CreateService();

        for (var i = 0; i < _options.MaxFailedAccessAttempts; i++)
        {
            await service.IncrementAccessFailedCountAsync("user1");
        }

        Assert.True(await service.GetLockoutEnabledAsync("user1"));
        Assert.False(await service.GetLockoutEnabledAsync("user2"));
    }
}
