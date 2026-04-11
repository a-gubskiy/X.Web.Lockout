using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using X.Web.Lockout.Services;

namespace X.Web.Lockout.Tests.Services;

public class MemoryLockoutServiceTests
{
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly LockoutOptions _options = new()
    {
        MaxFailedAccessAttempts = 3,
        DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5)
    };

    private MemoryLockoutService CreateService()
    {
        var cache = new MemoryCache(Options.Create(new MemoryCacheOptions()));

        return new MemoryLockoutService(_options, _timeProvider, cache);
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

    [Fact]
    public async Task RecordAccessFailedAttemptAsync_BelowThreshold_SetsExpirationToDefaultLockoutTimeSpan()
    {
        var cacheMock = new Mock<IMemoryCache>();
        var entryMock = new Mock<ICacheEntry>();
        entryMock.SetupAllProperties();
        cacheMock
            .Setup(c => c.CreateEntry(It.IsAny<object>()))
            .Returns(entryMock.Object);

        var service = new MemoryLockoutService(_options, _timeProvider, cacheMock.Object);

        await service.IncrementAccessFailedCountAsync("user1");

        Assert.Equal(_options.DefaultLockoutTimeSpan, entryMock.Object.AbsoluteExpirationRelativeToNow);
    }

    [Fact]
    public async Task RecordAccessFailedAttemptAsync_AtThreshold_SetsExpirationToLockoutDuration()
    {
        var cacheMock = new Mock<IMemoryCache>();
        var existingEntry = new LockoutEntry
        {
            AccessFailedCount = _options.MaxFailedAccessAttempts - 1
        };
        object? outVal = existingEntry;
        cacheMock.Setup(c => c.TryGetValue(It.IsAny<object>(), out outVal)).Returns(true);

        var entryMock = new Mock<ICacheEntry>();
        entryMock.SetupAllProperties();
        cacheMock.Setup(c => c.CreateEntry(It.IsAny<object>())).Returns(entryMock.Object);

        var service = new MemoryLockoutService(_options, _timeProvider, cacheMock.Object);

        await service.IncrementAccessFailedCountAsync("user1");

        // Threshold hit — entry now has LockoutEnd; expiration should equal the remaining lockout duration
        Assert.Equal(_options.DefaultLockoutTimeSpan, entryMock.Object.AbsoluteExpirationRelativeToNow);
    }

    [Fact]
    public async Task RecordAccessFailedAttemptAsync_WhenExistingLockoutEndIsInPast_FallsBackToDefaultLockoutTimeSpan()
    {
        // Existing entry has a LockoutEnd that has already passed.
        // GetEntryLifetime should ignore it and use DefaultLockoutTimeSpan as the sliding window.
        var cacheMock = new Mock<IMemoryCache>();
        var staleEntry = new LockoutEntry
        {
            AccessFailedCount = 1,
            LockoutEndDate = _timeProvider.GetUtcNow().AddMinutes(-10)
        };
        object? outVal = staleEntry;
        cacheMock.Setup(c => c.TryGetValue(It.IsAny<object>(), out outVal)).Returns(true);

        var entryMock = new Mock<ICacheEntry>();
        entryMock.SetupAllProperties();
        cacheMock.Setup(c => c.CreateEntry(It.IsAny<object>())).Returns(entryMock.Object);

        var service = new MemoryLockoutService(_options, _timeProvider, cacheMock.Object);

        await service.IncrementAccessFailedCountAsync("user1");

        Assert.Equal(_options.DefaultLockoutTimeSpan, entryMock.Object.AbsoluteExpirationRelativeToNow);
    }

    [Fact]
    public async Task Constructor_WithOptionsAndCacheOnly_UsesSystemTimeProvider()
    {
        var cache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
        var service = new MemoryLockoutService(_options, cache);

        for (var i = 0; i < _options.MaxFailedAccessAttempts; i++)
        {
            await service.IncrementAccessFailedCountAsync("user1");
        }

        Assert.True(await service.GetLockoutEnabledAsync("user1"));
    }

    [Fact]
    public async Task IncrementAccessFailedCountAsync_WithSizeLimitedCache_PersistsEntry()
    {
        var cache = new MemoryCache(Options.Create(new MemoryCacheOptions
        {
            SizeLimit = 10
        }));
        var service = new MemoryLockoutService(_options, _timeProvider, cache);

        await service.IncrementAccessFailedCountAsync("user1");
        await service.IncrementAccessFailedCountAsync("user1");

        Assert.False(await service.GetLockoutEnabledAsync("user1"));
    }
}
