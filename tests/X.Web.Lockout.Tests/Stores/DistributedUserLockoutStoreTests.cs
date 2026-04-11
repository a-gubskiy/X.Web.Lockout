using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using X.Web.Lockout.Stores;

namespace X.Web.Lockout.Tests.Stores;

public class DistributedUserLockoutStoreTests
{
    private readonly Mock<IUserStore<TestUser>> _userStoreMock = new();
    private readonly TestUser _user = new("user1");

    public DistributedUserLockoutStoreTests()
    {
        _userStoreMock
            .Setup(s => s.GetUserIdAsync(_user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_user.Id);
    }

    private DistributedUserLockoutStore<TestUser> CreateStore()
    {
        var cache = new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions()));

        return new DistributedUserLockoutStore<TestUser>(cache, _userStoreMock.Object);
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
    public async Task IncrementAccessFailedCountAsync_ParallelCalls_PreserveAllUpdates()
    {
        var coordinatedUserStore = new CoordinatedUserStore();
        var cache = new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions()));
        var store = new DistributedUserLockoutStore<TestUser>(cache, coordinatedUserStore);

        var firstIncrement = Task.Run(() => store.IncrementAccessFailedCountAsync(_user, CancellationToken.None));
        await coordinatedUserStore.FirstGetUserIdStarted;

        var secondIncrement = Task.Run(() => store.IncrementAccessFailedCountAsync(_user, CancellationToken.None));
        await coordinatedUserStore.SecondGetUserIdStarted;

        coordinatedUserStore.ReleaseGetUserId();

        await Task.WhenAll(firstIncrement, secondIncrement);

        var result = await store.GetAccessFailedCountAsync(_user, CancellationToken.None);

        Assert.Equal(2, result);
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

    [Fact]
    public async Task UpdateAsync_DelegatesToInnerStore()
    {
        _userStoreMock
            .Setup(s => s.UpdateAsync(_user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Success);

        var store = CreateStore();

        var result = await store.UpdateAsync(_user, CancellationToken.None);

        Assert.True(result.Succeeded);
        _userStoreMock.Verify(s => s.UpdateAsync(_user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetUserNameAsync_DelegatesToInnerStore()
    {
        _userStoreMock
            .Setup(s => s.GetUserNameAsync(_user, It.IsAny<CancellationToken>()))
            .ReturnsAsync("name");

        var store = CreateStore();

        var result = await store.GetUserNameAsync(_user, CancellationToken.None);

        Assert.Equal("name", result);
    }

    [Fact]
    public async Task SetUserNameAsync_DelegatesToInnerStore()
    {
        var store = CreateStore();

        await store.SetUserNameAsync(_user, "new-name", CancellationToken.None);

        _userStoreMock.Verify(
            s => s.SetUserNameAsync(_user, "new-name", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetNormalizedUserNameAsync_DelegatesToInnerStore()
    {
        _userStoreMock
            .Setup(s => s.GetNormalizedUserNameAsync(_user, It.IsAny<CancellationToken>()))
            .ReturnsAsync("NAME");

        var store = CreateStore();

        var result = await store.GetNormalizedUserNameAsync(_user, CancellationToken.None);

        Assert.Equal("NAME", result);
    }

    [Fact]
    public async Task SetNormalizedUserNameAsync_DelegatesToInnerStore()
    {
        var store = CreateStore();

        await store.SetNormalizedUserNameAsync(_user, "NEW-NAME", CancellationToken.None);

        _userStoreMock.Verify(
            s => s.SetNormalizedUserNameAsync(_user, "NEW-NAME", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FindByNameAsync_DelegatesToInnerStore()
    {
        _userStoreMock
            .Setup(s => s.FindByNameAsync("USER1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_user);

        var store = CreateStore();

        var result = await store.FindByNameAsync("USER1", CancellationToken.None);

        Assert.Same(_user, result);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var store = CreateStore();

        store.Dispose();
    }

    [Fact]
    public async Task GetLockoutEntry_WhenCachedJsonIsNullLiteral_ReturnsFreshEntry()
    {
        _userStoreMock
            .Setup(s => s.GetUserIdAsync(_user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_user.Id);

        var cacheMock = new Mock<IDistributedCache>();

        // Return the JSON string "null" so JsonSerializer.Deserialize<LockoutEntry> returns null,
        // exercising the "?? new LockoutEntry()" fallback.
        cacheMock
            .Setup(c => c.GetAsync($"lockout:{_user.Id}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes("null"));

        var store = new DistributedUserLockoutStore<TestUser>(cacheMock.Object, _userStoreMock.Object);

        var result = await store.GetLockoutEndDateAsync(_user, CancellationToken.None);

        Assert.Null(result);
    }
}
