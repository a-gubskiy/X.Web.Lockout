using System.Collections.Generic;
using System.Threading;

namespace X.Web.Lockout.Internal;

internal sealed class AsyncKeyedLock
{
    private readonly object _gate = new();
    private readonly Dictionary<string, LockState> _states = new(StringComparer.Ordinal);

    public async Task<Releaser> AcquireAsync(string key, CancellationToken cancellationToken)
    {
        LockState state;

        lock (_gate)
        {
            if (!_states.TryGetValue(key, out state!))
            {
                state = new LockState();
                _states.Add(key, state);
            }

            state.RefCount++;
        }

        try
        {
            await state.Semaphore.WaitAsync(cancellationToken);

            return new Releaser(this, key, state);
        }
        catch
        {
            ReleaseReference(key, state);
            throw;
        }
    }

    private void Release(string key, LockState state)
    {
        state.Semaphore.Release();
        ReleaseReference(key, state);
    }

    private void ReleaseReference(string key, LockState state)
    {
        lock (_gate)
        {
            state.RefCount--;

            if (state.RefCount == 0 &&
                _states.TryGetValue(key, out var current) &&
                ReferenceEquals(current, state))
            {
                _states.Remove(key);
            }
        }
    }

    internal readonly struct Releaser : IDisposable
    {
        private readonly AsyncKeyedLock _owner;
        private readonly string _key;
        private readonly LockState _state;

        public Releaser(AsyncKeyedLock owner, string key, LockState state)
        {
            _owner = owner;
            _key = key;
            _state = state;
        }

        public void Dispose()
        {
            _owner.Release(_key, _state);
        }
    }

    internal sealed class LockState
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public int RefCount { get; set; }
    }
}
