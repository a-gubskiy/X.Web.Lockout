using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Time.Testing;
using X.Web.Lockout.Services;

namespace X.Web.Lockout.Tests.Services;

public class LockoutServiceBaseTests
{
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly LockoutOptions _options = new()
    {
        MaxFailedAccessAttempts = 2,
        DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5)
    };

    [Fact]
    public async Task IncrementAccessFailedCountAsync_ParallelCalls_DoNotLoseUpdates()
    {
        var service = new CoordinatedLockoutService(_options, _timeProvider);

        var firstAttempt = Task.Run(() => service.IncrementAccessFailedCountAsync("user1"));
        await service.FirstLoadStarted;

        var secondAttempt = Task.Run(() => service.IncrementAccessFailedCountAsync("user1"));
        await Task.WhenAny(service.SecondLoadStarted, Task.Delay(TimeSpan.FromMilliseconds(100)));

        service.ReleaseLoad();

        await Task.WhenAll(firstAttempt, secondAttempt);

        Assert.Equal(2, service.AccessFailedCount);
        Assert.True(await service.GetLockoutEnabledAsync("user1"));
    }

    private sealed class CoordinatedLockoutService : LockoutServiceBase
    {
        private readonly TaskCompletionSource<bool> _firstLoadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _secondLoadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _releaseLoad = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private LockoutEntry? _entry;
        private int _loadCallCount;

        public CoordinatedLockoutService(LockoutOptions options, TimeProvider timeProvider)
            : base(options, timeProvider)
        {
        }

        public Task FirstLoadStarted => _firstLoadStarted.Task;

        public Task SecondLoadStarted => _secondLoadStarted.Task;

        public int AccessFailedCount => _entry?.AccessFailedCount ?? 0;

        public void ReleaseLoad()
        {
            _releaseLoad.TrySetResult(true);
        }

        protected override async Task<LockoutEntry?> LoadAsync(string userId, CancellationToken cancellationToken)
        {
            var snapshot = _entry is null
                ? null
                : new LockoutEntry
                {
                    AccessFailedCount = _entry.AccessFailedCount,
                    LockoutEndDate = _entry.LockoutEndDate
                };

            var callNumber = Interlocked.Increment(ref _loadCallCount);

            if (callNumber == 1)
            {
                _firstLoadStarted.TrySetResult(true);
                await _releaseLoad.Task.WaitAsync(cancellationToken);
            }
            else if (callNumber == 2)
            {
                _secondLoadStarted.TrySetResult(true);
                await _releaseLoad.Task.WaitAsync(cancellationToken);
            }

            return snapshot;
        }

        protected override Task SaveAsync(string userId, LockoutEntry entry, CancellationToken cancellationToken)
        {
            _entry = new LockoutEntry
            {
                AccessFailedCount = entry.AccessFailedCount,
                LockoutEndDate = entry.LockoutEndDate
            };

            return Task.CompletedTask;
        }

        protected override Task RemoveAsync(string userId, CancellationToken cancellationToken)
        {
            _entry = null;

            return Task.CompletedTask;
        }
    }
}
