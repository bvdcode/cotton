// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    internal class KeyedAsyncGate<TKey>(IEqualityComparer<TKey>? comparer = null)
        where TKey : notnull
    {
        private readonly Lock _gateLock = new();
        private readonly Dictionary<TKey, KeyedAsyncGateEntry> _gates = new(comparer);

        public int Count
        {
            get
            {
                lock (_gateLock)
                {
                    return _gates.Count;
                }
            }
        }

        public async ValueTask<IAsyncDisposable> EnterAsync(
            TKey key,
            CancellationToken cancellationToken)
        {
            KeyedAsyncGateEntry entry;
            lock (_gateLock)
            {
                if (!_gates.TryGetValue(key, out entry!))
                {
                    entry = new KeyedAsyncGateEntry();
                    _gates.Add(key, entry);
                }

                entry.ReferenceCount++;
            }

            bool acquired = false;
            try
            {
                await entry.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                acquired = true;
                return new AsyncGateLease(() => Release(key, entry));
            }
            finally
            {
                if (!acquired)
                {
                    ReleaseReference(key, entry);
                }
            }
        }

        private void Release(TKey key, KeyedAsyncGateEntry entry)
        {
            entry.Gate.Release();
            ReleaseReference(key, entry);
        }

        private void ReleaseReference(TKey key, KeyedAsyncGateEntry entry)
        {
            lock (_gateLock)
            {
                entry.ReferenceCount--;
                if (entry.ReferenceCount != 0)
                {
                    return;
                }

                _gates.Remove(key);
                entry.Gate.Dispose();
            }
        }
    }
}
