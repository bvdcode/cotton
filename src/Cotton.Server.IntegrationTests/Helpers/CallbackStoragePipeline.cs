// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Pipelines;

namespace Cotton.Server.IntegrationTests.Helpers
{
    public class CallbackStoragePipeline(IStoragePipeline inner, Func<int, Task> beforeRead) : IStoragePipeline
    {
        public int ReadCount { get; private set; }

        public int WriteCount { get; private set; }

        public async Task<Stream> ReadAsync(string uid, PipelineContext? context = null)
        {
            ReadCount++;
            await beforeRead(ReadCount);
            return await inner.ReadAsync(uid, context);
        }

        public Task<bool> DeleteAsync(string uid) => inner.DeleteAsync(uid);

        public Task<bool> ExistsAsync(string uid) => inner.ExistsAsync(uid);

        public Task<long> GetSizeAsync(string uid) => inner.GetSizeAsync(uid);

        public Task<long> WriteAsync(string uid, Stream stream, PipelineContext? context = null,
            CancellationToken cancellationToken = default)
        {
            WriteCount++;
            return inner.WriteAsync(uid, stream, context, cancellationToken);
        }

        public IAsyncEnumerable<string> ListAllKeysAsync(CancellationToken ct = default) => inner.ListAllKeysAsync(ct);

        public IAsyncEnumerable<string> ListKeysByPrefixAsync(char prefix, CancellationToken ct = default) =>
            inner.ListKeysByPrefixAsync(prefix, ct);
    }
}
