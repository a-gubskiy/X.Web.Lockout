using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Time.Testing;
using Moq;
using X.Web.Lockout.Services;

namespace X.Web.Lockout.Tests.Services;

public class StoreLockoutServiceTests
{
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly LockoutOptions _options = new()
    {
        MaxFailedAccessAttempts = 3,
        DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5)
    };

    private readonly Mock<IUserLockoutStore<TestUser>> _storeMock = new();

    private StoreLockoutService<TestUser> CreateService() =>
        new(_options, _timeProvider, _storeMock.Object);

    public StoreLockoutServiceTests()
    {
        var user = new TestUser("user1");

        _storeMock
            .Setup(s => s.FindByIdAsync("user1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _storeMock
            .Setup(s => s.GetUserIdAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user.Id);
    }

    [Fact]
    public async Task GetLockoutEnabledAsync_NoLockoutEnd_ReturnsFalse()
    {
        _storeMock
            .Setup(s => s.GetLockoutEndDateAsync(It.IsAny<TestUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTimeOffset?)null);

        var service = CreateService();

        var result = await service.GetLockoutEnabledAsync("user1");

        Assert.False(result);
    }

    [Fact]
    public async Task GetLockoutEnabledAsync_LockoutEndInFuture_ReturnsTrue()
    {
        var futureDate = _timeProvider.GetUtcNow().AddMinutes(5);

        _storeMock
            .Setup(s => s.GetLockoutEndDateAsync(It.IsAny<TestUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(futureDate);

        var service = CreateService();

        var result = await service.GetLockoutEnabledAsync("user1");

        Assert.True(result);
    }

    [Fact]
    public async Task GetLockoutEnabledAsync_LockoutEndInPast_ReturnsFalse()
    {
        var pastDate = _timeProvider.GetUtcNow().AddMinutes(-1);

        _storeMock
            .Setup(s => s.GetLockoutEndDateAsync(It.IsAny<TestUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pastDate);

        var service = CreateService();

        var result = await service.GetLockoutEnabledAsync("user1");

        Assert.False(result);
    }

    [Fact]
    public async Task RecordAccessFailedAttemptAsync_BelowThreshold_DoesNotSetLockoutEnd()
    {
        _storeMock
            .Setup(s => s.IncrementAccessFailedCountAsync(It.IsAny<TestUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service = CreateService();

        await service.IncrementAccessFailedCountAsync("user1");

        _storeMock.Verify(
            s => s.SetLockoutEndDateAsync(It.IsAny<TestUser>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RecordAccessFailedAttemptAsync_AtThreshold_SetsLockoutEnd()
    {
        _storeMock
            .Setup(s => s.IncrementAccessFailedCountAsync(It.IsAny<TestUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_options.MaxFailedAccessAttempts);

        var service = CreateService();

        await service.IncrementAccessFailedCountAsync("user1");

        _storeMock.Verify(
            s => s.SetLockoutEndDateAsync(
                It.IsAny<TestUser>(),
                It.Is<DateTimeOffset?>(d => d.HasValue && d.Value > _timeProvider.GetUtcNow()),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RecordAccessFailedAttemptAsync_AboveThreshold_SetsLockoutEnd()
    {
        _storeMock
            .Setup(s => s.IncrementAccessFailedCountAsync(It.IsAny<TestUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_options.MaxFailedAccessAttempts + 1);

        var service = CreateService();

        await service.IncrementAccessFailedCountAsync("user1");

        _storeMock.Verify(
            s => s.SetLockoutEndDateAsync(
                It.IsAny<TestUser>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResetAccessFailedAttemptsAsync_ResetsCountAndClearsLockoutEnd()
    {
        var service = CreateService();

        await service.ResetAccessFailedCountAsync("user1");

        _storeMock.Verify(
            s => s.ResetAccessFailedCountAsync(It.IsAny<TestUser>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _storeMock.Verify(
            s => s.SetLockoutEndDateAsync(It.IsAny<TestUser>(), null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Constructor_WithOptionsAndStoreOnly_UsesSystemTimeProvider()
    {
        _storeMock
            .Setup(s => s.GetLockoutEndDateAsync(It.IsAny<TestUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTimeOffset.UtcNow.AddMinutes(5));

        var service = new StoreLockoutService<TestUser>(_options, _storeMock.Object);

        var result = await service.GetLockoutEnabledAsync("user1");

        Assert.True(result);
    }

    [Fact]
    public async Task GetLockoutEnabledAsync_UserNotFound_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetLockoutEnabledAsync("unknown"));
    }

    [Fact]
    public async Task RecordAccessFailedAttemptAsync_UserNotFound_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.IncrementAccessFailedCountAsync("unknown"));
    }

    [Fact]
    public async Task ResetAccessFailedAttemptsAsync_UserNotFound_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ResetAccessFailedCountAsync("unknown"));
    }
}
