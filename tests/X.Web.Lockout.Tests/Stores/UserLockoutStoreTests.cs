using Microsoft.AspNetCore.Identity;
using Moq;
using X.Web.Lockout.Stores;

namespace X.Web.Lockout.Tests.Stores;

public class UserLockoutStoreTests
{
    private readonly Mock<IUserStore<TestUser>> _userStoreMock = new();
    private readonly TestUser _user = new("user1");

    private UserLockoutStore<TestUser> CreateStore()
    {
        _userStoreMock
            .Setup(s => s.GetUserIdAsync(_user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_user.Id);

        return new UserLockoutStore<TestUser>(_userStoreMock.Object);
    }

    // Lockout end date

    [Fact]
    public async Task GetLockoutEndDateAsync_Default_ReturnsNull()
    {
        var store = CreateStore();

        var result = await store.GetLockoutEndDateAsync(_user, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task SetLockoutEndDateAsync_Persists()
    {
        var store = CreateStore();
        var expected = DateTimeOffset.UtcNow.AddMinutes(5);

        await store.SetLockoutEndDateAsync(_user, expected, CancellationToken.None);
        var result = await store.GetLockoutEndDateAsync(_user, CancellationToken.None);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task SetLockoutEndDateAsync_Null_ClearsValue()
    {
        var store = CreateStore();

        await store.SetLockoutEndDateAsync(_user, DateTimeOffset.UtcNow.AddMinutes(5), CancellationToken.None);
        await store.SetLockoutEndDateAsync(_user, null, CancellationToken.None);
        var result = await store.GetLockoutEndDateAsync(_user, CancellationToken.None);

        Assert.Null(result);
    }

    // Access failed count

    [Fact]
    public async Task GetAccessFailedCountAsync_Default_ReturnsZero()
    {
        var store = CreateStore();

        var result = await store.GetAccessFailedCountAsync(_user, CancellationToken.None);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task IncrementAccessFailedCountAsync_IncrementsAndReturnsNewCount()
    {
        var store = CreateStore();

        var count1 = await store.IncrementAccessFailedCountAsync(_user, CancellationToken.None);
        var count2 = await store.IncrementAccessFailedCountAsync(_user, CancellationToken.None);
        var count3 = await store.IncrementAccessFailedCountAsync(_user, CancellationToken.None);

        Assert.Equal(1, count1);
        Assert.Equal(2, count2);
        Assert.Equal(3, count3);
    }

    [Fact]
    public async Task ResetAccessFailedCountAsync_ResetsToZero()
    {
        var store = CreateStore();

        await store.IncrementAccessFailedCountAsync(_user, CancellationToken.None);
        await store.IncrementAccessFailedCountAsync(_user, CancellationToken.None);
        await store.ResetAccessFailedCountAsync(_user, CancellationToken.None);

        var result = await store.GetAccessFailedCountAsync(_user, CancellationToken.None);

        Assert.Equal(0, result);
    }

    // Lockout enabled

    [Fact]
    public async Task GetLockoutEnabledAsync_Default_ReturnsFalse()
    {
        var store = CreateStore();

        var result = await store.GetLockoutEnabledAsync(_user, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task SetLockoutEnabledAsync_True_Persists()
    {
        var store = CreateStore();

        await store.SetLockoutEnabledAsync(_user, true, CancellationToken.None);
        var result = await store.GetLockoutEnabledAsync(_user, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task SetLockoutEnabledAsync_FalseAfterTrue_Persists()
    {
        var store = CreateStore();

        await store.SetLockoutEnabledAsync(_user, true, CancellationToken.None);
        await store.SetLockoutEnabledAsync(_user, false, CancellationToken.None);
        var result = await store.GetLockoutEnabledAsync(_user, CancellationToken.None);

        Assert.False(result);
    }

    // User isolation

    [Fact]
    public async Task LockoutData_IsolatedPerUser()
    {
        var user2 = new TestUser("user2");

        _userStoreMock
            .Setup(s => s.GetUserIdAsync(user2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user2.Id);

        var store = CreateStore();

        await store.IncrementAccessFailedCountAsync(_user, CancellationToken.None);
        await store.IncrementAccessFailedCountAsync(_user, CancellationToken.None);
        await store.SetLockoutEndDateAsync(_user, DateTimeOffset.UtcNow.AddMinutes(5), CancellationToken.None);
        await store.SetLockoutEnabledAsync(_user, true, CancellationToken.None);

        Assert.Equal(0, await store.GetAccessFailedCountAsync(user2, CancellationToken.None));
        Assert.Null(await store.GetLockoutEndDateAsync(user2, CancellationToken.None));
        Assert.False(await store.GetLockoutEnabledAsync(user2, CancellationToken.None));
    }

    // Delegation to inner IUserStore

    [Fact]
    public async Task GetUserIdAsync_DelegatesToInnerStore()
    {
        var store = CreateStore();

        var result = await store.GetUserIdAsync(_user, CancellationToken.None);

        Assert.Equal("user1", result);
        _userStoreMock.Verify(s => s.GetUserIdAsync(_user, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task FindByIdAsync_DelegatesToInnerStore()
    {
        _userStoreMock
            .Setup(s => s.FindByIdAsync("user1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_user);

        var store = CreateStore();

        var result = await store.FindByIdAsync("user1", CancellationToken.None);

        Assert.Same(_user, result);
    }

    [Fact]
    public async Task CreateAsync_DelegatesToInnerStore()
    {
        _userStoreMock
            .Setup(s => s.CreateAsync(_user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Success);

        var store = CreateStore();

        var result = await store.CreateAsync(_user, CancellationToken.None);

        Assert.True(result.Succeeded);
        _userStoreMock.Verify(s => s.CreateAsync(_user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_DelegatesToInnerStore()
    {
        _userStoreMock
            .Setup(s => s.DeleteAsync(_user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Success);

        var store = CreateStore();

        var result = await store.DeleteAsync(_user, CancellationToken.None);

        Assert.True(result.Succeeded);
        _userStoreMock.Verify(s => s.DeleteAsync(_user, It.IsAny<CancellationToken>()), Times.Once);
    }
}
