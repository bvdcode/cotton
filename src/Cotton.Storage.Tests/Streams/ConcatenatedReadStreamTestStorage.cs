// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;
using Cotton.Storage.Pipelines;
using System.Runtime.CompilerServices;

namespace Cotton.Storage.Tests.Streams
{
    internal class ConcatenatedReadStreamTestStorage : IStoragePipeline
    {
        private readonly Dictionary<string, byte[]> _data = [];

        public void AddData(string uid, byte[] data)
        {
            _data[uid] = data;
        }

        public Task<bool> DeleteAsync(string uid)
        {
            return Task.FromResult(_data.Remove(uid));
        }

        public Task<bool> ExistsAsync(string uid)
        {
            return Task.FromResult(_data.ContainsKey(uid));
        }

        public Task<long> GetSizeAsync(string uid)
        {
            return Task.FromResult(_data.TryGetValue(uid, out byte[]? data) ? data.Length : 0L);
        }

        public Task<Stream> ReadAsync(string uid, PipelineContext? context = null)
        {
            if (!_data.TryGetValue(uid, out byte[]? data))
            {
                throw new FileNotFoundException($"UID not found: {uid}");
            }

            return Task.FromResult<Stream>(new MemoryStream(data));
        }

        public Task<long> WriteAsync(
            string uid,
            Stream stream,
            PipelineContext? context = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async IAsyncEnumerable<string> ListAllKeysAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (string key in _data.Keys)
            {
                yield return key;
            }

            await Task.CompletedTask;
        }
    }
}
