using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Time.Testing;
using Moq;
using X.Web.Lockout.Services;

namespace X.Web.Lockout.Tests.Services;

public class StoreUserLockoutServiceTests
{
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly LockoutOptions _options = new()
    {
        MaxFailedAccessAttempts = 3,
        DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5)
    };

    private readonly Mock<IUserLockoutStore<TestUser>> _storeMock = new();
    private readonly TestUser _user = new("user1");

    private StoreUserLockoutService<TestUser> CreateService() =>
        new(_options, _timeProvider, _storeMock.Object);

    // GetLockoutEnabledAsync

    [Fact]
    public async Task GetLockoutEnabledAsync_NoLockoutEnd_ReturnsFalse()
    {
        _storeMock
            .Setup(s => s.GetLockoutEndDateAsync(_user, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTimeOffset?)null);

        var service = CreateService();

        var result = await service.GetLockoutEnabledAsync(_user);

        Assert.False(result);
    }

    [Fact]
    public async Task GetLockoutEnabledAsync_LockoutEndInFuture_ReturnsTrue()
    {
        _storeMock
            .Setup(s => s.GetLockoutEndDateAsync(_user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_timeProvider.GetUtcNow().AddMinutes(5));

        var service = CreateService();

        var result = await service.GetLockoutEnabledAsync(_user);

        Assert.True(result);
    }

    [Fact]
    public async Task GetLockoutEnabledAsync_LockoutEndInPast_ReturnsFalse()
    {
        _storeMock
            .Setup(s => s.GetLockoutEndDateAsync(_user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_timeProvider.GetUtcNow().AddMinutes(-1));

        var service = CreateService();

        var result = await service.GetLockoutEnabledAsync(_user);

        Assert.False(result);
    }

    [Fact]
    public async Task GetLockoutEnabledAsync_LockoutEndExactlyNow_ReturnsFalse()
    {
        _storeMock
            .Setup(s => s.GetLockoutEndDateAsync(_user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_timeProvider.GetUtcNow());

        var service = CreateService();

        var result = await service.GetLockoutEnabledAsync(_user);

        Assert.False(result);
    }

    // IncrementAccessFailedCountAsync

    [Fact]
    public async Task IncrementAccessFailedCountAsync_BelowThreshold_DoesNotSetLockoutEnd()
    {
        _storeMock
            .Setup(s => s.IncrementAccessFailedCountAsync(_user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service = CreateService();

        await service.IncrementAccessFailedCountAsync(_user);

        _storeMock.Verify(
            s => s.SetLockoutEndDateAsync(It.IsAny<TestUser>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task IncrementAccessFailedCountAsync_AtThreshold_SetsLockoutEnd()
    {
        _storeMock
            .Setup(s => s.IncrementAccessFailedCountAsync(_user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_options.MaxFailedAccessAttempts);

        var service = CreateService();

        await service.IncrementAccessFailedCountAsync(_user);

        var expectedLockoutEnd = _timeProvider.GetUtcNow().Add(_options.DefaultLockoutTimeSpan);

        _storeMock.Verify(
            s => s.SetLockoutEndDateAsync(
                _user,
                It.Is<DateTimeOffset?>(d => d.HasValue && d.Value == expectedLockoutEnd),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IncrementAccessFailedCountAsync_AboveThreshold_SetsLockoutEnd()
    {
        _storeMock
            .Setup(s => s.IncrementAccessFailedCountAsync(_user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_options.MaxFailedAccessAttempts + 1);

        var service = CreateService();

        await service.IncrementAccessFailedCountAsync(_user);

        _storeMock.Verify(
            s => s.SetLockoutEndDateAsync(_user, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IncrementAccessFailedCountAsync_AlwaysCallsStoreIncrement()
    {
        _storeMock
            .Setup(s => s.IncrementAccessFailedCountAsync(_user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service = CreateService();

        await service.IncrementAccessFailedCountAsync(_user);

        _storeMock.Verify(
            s => s.IncrementAccessFailedCountAsync(_user, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ResetAccessFailedCountAsync

    [Fact]
    public async Task ResetAccessFailedCountAsync_ResetsCountAndClearsLockoutEnd()
    {
        var service = CreateService();

        await service.ResetAccessFailedCountAsync(_user);

        _storeMock.Verify(
            s => s.ResetAccessFailedCountAsync(_user, It.IsAny<CancellationToken>()),
            Times.Once);

        _storeMock.Verify(
            s => s.SetLockoutEndDateAsync(_user, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // CancellationToken propagation

    [Fact]
    public async Task GetLockoutEnabledAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _storeMock
            .Setup(s => s.GetLockoutEndDateAsync(_user, token))
            .ReturnsAsync((DateTimeOffset?)null);

        var service = CreateService();

        await service.GetLockoutEnabledAsync(_user, token);

        _storeMock.Verify(s => s.GetLockoutEndDateAsync(_user, token), Times.Once);
    }

    [Fact]
    public async Task IncrementAccessFailedCountAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _storeMock
            .Setup(s => s.IncrementAccessFailedCountAsync(_user, token))
            .ReturnsAsync(_options.MaxFailedAccessAttempts);

        var service = CreateService();

        await service.IncrementAccessFailedCountAsync(_user, token);

        _storeMock.Verify(s => s.IncrementAccessFailedCountAsync(_user, token), Times.Once);
        _storeMock.Verify(s => s.SetLockoutEndDateAsync(_user, It.IsAny<DateTimeOffset?>(), token), Times.Once);
    }

    [Fact]
    public async Task ResetAccessFailedCountAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var service = CreateService();

        await service.ResetAccessFailedCountAsync(_user, token);

        _storeMock.Verify(s => s.ResetAccessFailedCountAsync(_user, token), Times.Once);
        _storeMock.Verify(s => s.SetLockoutEndDateAsync(_user, null, token), Times.Once);
    }

    // Constructor overload

    [Fact]
    public async Task Constructor_WithOptionsAndStoreOnly_UsesSystemTimeProvider()
    {
        _storeMock
            .Setup(s => s.GetLockoutEndDateAsync(_user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTimeOffset.UtcNow.AddMinutes(5));

        var service = new StoreUserLockoutService<TestUser>(_options, _storeMock.Object);

        var result = await service.GetLockoutEnabledAsync(_user);

        Assert.True(result);
    }
}
