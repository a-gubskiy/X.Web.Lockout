using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Time.Testing;
using X.Web.Lockout.Services;

namespace X.Web.Lockout.Tests.Services;

public class LockoutServiceTests
{
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly LockoutOptions _options = new()
    {
        MaxFailedAccessAttempts = 3,
        DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5)
    };

    private LockoutService CreateService() => new(_options, _timeProvider);

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

        await service.RecordAccessFailedAttemptAsync("user1");
        await service.RecordAccessFailedAttemptAsync("user1");

        var result = await service.GetLockoutEnabledAsync("user1");

        Assert.False(result);
    }

    [Fact]
    public async Task GetLockoutEnabledAsync_AtThreshold_ReturnsTrue()
    {
        var service = CreateService();

        for (var i = 0; i < _options.MaxFailedAccessAttempts; i++)
        {
            await service.RecordAccessFailedAttemptAsync("user1");
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
            await service.RecordAccessFailedAttemptAsync("user1");
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
            await service.RecordAccessFailedAttemptAsync("user1");
        }

        Assert.True(await service.GetLockoutEnabledAsync("user1"));

        await service.ResetAccessFailedAttemptsAsync("user1");

        Assert.False(await service.GetLockoutEnabledAsync("user1"));
    }

    [Fact]
    public async Task ResetAccessFailedAttemptsAsync_ResetsCounter()
    {
        var service = CreateService();

        await service.RecordAccessFailedAttemptAsync("user1");
        await service.RecordAccessFailedAttemptAsync("user1");
        await service.ResetAccessFailedAttemptsAsync("user1");

        // Should need full MaxFailedAccessAttempts again to lock out
        await service.RecordAccessFailedAttemptAsync("user1");
        await service.RecordAccessFailedAttemptAsync("user1");

        Assert.False(await service.GetLockoutEnabledAsync("user1"));
    }

    [Fact]
    public async Task Constructor_WithOptionsOnly_UsesSystemTimeProvider()
    {
        var service = new LockoutService(_options);

        for (var i = 0; i < _options.MaxFailedAccessAttempts; i++)
        {
            await service.RecordAccessFailedAttemptAsync("user1");
        }

        Assert.True(await service.GetLockoutEnabledAsync("user1"));
    }

    [Fact]
    public async Task RecordAccessFailedAttemptAsync_IsolatesUsers()
    {
        var service = CreateService();

        for (var i = 0; i < _options.MaxFailedAccessAttempts; i++)
        {
            await service.RecordAccessFailedAttemptAsync("user1");
        }

        Assert.True(await service.GetLockoutEnabledAsync("user1"));
        Assert.False(await service.GetLockoutEnabledAsync("user2"));
    }
}
