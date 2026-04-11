using Microsoft.AspNetCore.Identity;

namespace X.Web.Lockout.Tests;

internal sealed class CoordinatedUserStore : IUserStore<TestUser>
{
    private readonly TaskCompletionSource<bool> _firstGetUserIdStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _secondGetUserIdStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _releaseGetUserId = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int _getUserIdCallCount;

    public Task FirstGetUserIdStarted => _firstGetUserIdStarted.Task;

    public Task SecondGetUserIdStarted => _secondGetUserIdStarted.Task;

    public void ReleaseGetUserId()
    {
        _releaseGetUserId.TrySetResult(true);
    }

    public async Task<string> GetUserIdAsync(TestUser user, CancellationToken cancellationToken)
    {
        var callNumber = Interlocked.Increment(ref _getUserIdCallCount);

        if (callNumber == 1)
        {
            _firstGetUserIdStarted.TrySetResult(true);
            await _secondGetUserIdStarted.Task.WaitAsync(cancellationToken);
        }
        else if (callNumber == 2)
        {
            _secondGetUserIdStarted.TrySetResult(true);
        }

        await _releaseGetUserId.Task.WaitAsync(cancellationToken);

        return user.Id;
    }

    public void Dispose()
    {
    }

    public Task<string?> GetUserNameAsync(TestUser user, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task SetUserNameAsync(TestUser user, string? userName, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<string?> GetNormalizedUserNameAsync(TestUser user, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task SetNormalizedUserNameAsync(TestUser user, string? normalizedName, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IdentityResult> CreateAsync(TestUser user, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IdentityResult> UpdateAsync(TestUser user, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IdentityResult> DeleteAsync(TestUser user, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<TestUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<TestUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
