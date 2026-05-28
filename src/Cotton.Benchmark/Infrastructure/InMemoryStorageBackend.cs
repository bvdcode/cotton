// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;
using System.Runtime.CompilerServices;

namespace Cotton.Benchmark.Infrastructure
{
    /// <summary>
    /// In-memory storage backend used to isolate CPU-bound pipeline benchmarks from disk latency.
    /// </summary>
    internal sealed class InMemoryStorageBackend : IStorageBackend
    {
        private readonly Dictionary<string, byte[]> _storage = [];

        public void CleanupTempFiles(TimeSpan ttl)
        {
            // No temporary files are created by this backend.
        }

        public Task<bool> DeleteAsync(string uid)
        {
            return Task.FromResult(_storage.Remove(uid));
        }

        public Task<bool> ExistsAsync(string uid)
        {
            return Task.FromResult(_storage.ContainsKey(uid));
        }

        public Task<long> GetSizeAsync(string uid)
        {
            return Task.FromResult(_storage.TryGetValue(uid, out byte[]? data) ? data.LongLength : 0L);
        }

        public Task<Stream> ReadAsync(string uid)
        {
            if (!_storage.TryGetValue(uid, out byte[]? data))
            {
                throw new FileNotFoundException($"UID not found: {uid}");
            }

            return Task.FromResult<Stream>(new MemoryStream(data, writable: false));
        }

        public Task WriteAsync(string uid, Stream stream, bool overwrite = false)
        {
            if (!overwrite && _storage.ContainsKey(uid))
            {
                return Task.CompletedTask;
            }

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            _storage[uid] = ms.ToArray();
            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<string> ListAllKeysAsync([EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (string key in _storage.Keys.ToArray())
            {
                ct.ThrowIfCancellationRequested();
                yield return key;
            }

            await Task.CompletedTask;
        }
    }
}
