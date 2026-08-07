// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using Microsoft.Extensions.Caching.Memory;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class UserStorageQuotaCacheTests
    {
        [Test]
        public async Task GetOrLoadAsync_DoesNotOverwriteNewerUsageWithStaleLoad()
        {
            using MemoryCache memoryCache = new(new MemoryCacheOptions());
            UserStorageQuotaCache cache = new(memoryCache);
            Guid userId = Guid.NewGuid();
            TaskCompletionSource loadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource releaseLoad = new(TaskCreationOptions.RunContinuationsAsynchronously);

            Task<long> staleLoad = cache.GetOrLoadAsync(
                userId,
                async cancellationToken =>
                {
                    loadStarted.TrySetResult();
                    await releaseLoad.Task.WaitAsync(cancellationToken);
                    return 0;
                },
                CancellationToken.None);

            await loadStarted.Task;
            cache.Set(userId, 6);
            releaseLoad.TrySetResult();

            Assert.That(await staleLoad, Is.EqualTo(6));
            Assert.That(
                await cache.GetOrLoadAsync(userId, _ => Task.FromResult(0L), CancellationToken.None),
                Is.EqualTo(6));
        }
    }
}
