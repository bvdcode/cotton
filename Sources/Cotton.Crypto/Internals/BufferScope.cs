﻿// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using System.Buffers;
using System.Collections.Concurrent;

namespace Cotton.Crypto.Internals
{
    internal class BufferScope(ArrayPool<byte> pool, int maxCount, long maxBytes) : IDisposable
    {
        private int _count;
        private long _bytes;
        private int _disposed;
        private readonly ConcurrentBag<byte[]> _free = [];
        private readonly ConcurrentBag<byte[]> _tracked = [];
        private readonly ArrayPool<byte> _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        private readonly ConcurrentDictionary<byte[], byte?> _active = new(ReferenceEqualityComparer<byte[]>.Instance);
        private readonly int _maxCount = maxCount > 0 ? maxCount : throw new ArgumentOutOfRangeException(nameof(maxCount));
        private readonly long _maxBytes = maxBytes > 0 ? maxBytes : throw new ArgumentOutOfRangeException(nameof(maxBytes));

        public byte[] Rent(int minimumLength)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumLength);
            ThrowIfDisposed();
            if (_free.TryTake(out var reused))
            {
                _active[reused] = null;
                var newCountReuse = Interlocked.Increment(ref _count);
                var newBytesReuse = Interlocked.Add(ref _bytes, reused.Length);
                if (newCountReuse > _maxCount || newBytesReuse > _maxBytes)
                {
                    _active.TryRemove(reused, out _);
                    Interlocked.Decrement(ref _count);
                    Interlocked.Add(ref _bytes, -reused.Length);
                    _free.Add(reused);
                }
                else
                {
                    return reused;
                }
            }
            var arr = _pool.Rent(minimumLength);
            _active[arr] = null;
            var newCount = Interlocked.Increment(ref _count);
            var newBytes = Interlocked.Add(ref _bytes, arr.Length);
            if (newCount > _maxCount || newBytes > _maxBytes)
            {
                _active.TryRemove(arr, out _);
                Interlocked.Decrement(ref _count);
                Interlocked.Add(ref _bytes, -arr.Length);
                _pool.Return(arr, clearArray: false);
                throw new InvalidOperationException("BufferScope limit exceeded.");
            }
            _tracked.Add(arr);
            return arr;
        }

        public void Recycle(byte[] buffer)
        {
            ThrowIfDisposed();
            if (buffer is null)
            {
                return;
            }
            if (_active.TryRemove(buffer, out _))
            {
                Interlocked.Decrement(ref _count);
                Interlocked.Add(ref _bytes, -buffer.Length);
            }
            _free.Add(buffer);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            // Deduplicate tracked and free to avoid double return
            var unique = new HashSet<byte[]>(ReferenceEqualityComparer<byte[]>.Instance);
            while (_tracked.TryTake(out var arr1)) unique.Add(arr1);
            while (_free.TryTake(out var arr2)) unique.Add(arr2);
            foreach (var kv in _active.Keys)
            {
                unique.Add(kv);
            }
            foreach (var arr in unique)
            {
                Array.Clear(arr, 0, arr.Length);
                _pool.Return(arr, clearArray: false);
            }
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, nameof(BufferScope));
        }
    }
}
