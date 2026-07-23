// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Configuration;
using Cotton.Server.Services;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests;

public class HlsTranscodeCoordinatorTests
{
    [Test]
    public async Task EnterTranscodeAsync_QueuesAboveConfiguredLimit()
    {
        HlsTranscodeCoordinator coordinator = CreateCoordinator(maxConcurrentTranscodes: 2);
        Task<IAsyncDisposable> queued;

        await using (IAsyncDisposable first =
            await coordinator.EnterTranscodeAsync(CancellationToken.None))
        await using (IAsyncDisposable second =
            await coordinator.EnterTranscodeAsync(CancellationToken.None))
        {
            queued = coordinator.EnterTranscodeAsync(CancellationToken.None).AsTask();
            Assert.That(queued.IsCompleted, Is.False);
        }

        await using IAsyncDisposable admitted =
            await queued.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task EnterTranscodeAsync_CancelledWaiterDoesNotConsumePermit()
    {
        HlsTranscodeCoordinator coordinator = CreateCoordinator(maxConcurrentTranscodes: 1);
        using CancellationTokenSource cancellation = new();

        await using (IAsyncDisposable first =
            await coordinator.EnterTranscodeAsync(CancellationToken.None))
        {
            Task<IAsyncDisposable> cancelled = coordinator
                .EnterTranscodeAsync(cancellation.Token)
                .AsTask();
            cancellation.Cancel();

            Assert.CatchAsync<OperationCanceledException>(
                async () => await cancelled.WaitAsync(TimeSpan.FromSeconds(1)));
        }

        await using IAsyncDisposable admitted = await coordinator
            .EnterTranscodeAsync(CancellationToken.None)
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task EnterSegmentAsync_SerializesSameKeyWithoutBlockingOtherKeys()
    {
        HlsTranscodeCoordinator coordinator = CreateCoordinator(maxConcurrentTranscodes: 1);
        Task<IAsyncDisposable> sameKey;

        await using (IAsyncDisposable first =
            await coordinator.EnterSegmentAsync("segment-a", CancellationToken.None))
        {
            sameKey = coordinator.EnterSegmentAsync("segment-a", CancellationToken.None).AsTask();
            await using IAsyncDisposable otherKey =
                await coordinator.EnterSegmentAsync("segment-b", CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(sameKey.IsCompleted, Is.False);
                Assert.That(coordinator.SegmentGateCount, Is.EqualTo(2));
            });
        }

        await using (IAsyncDisposable admitted =
            await sameKey.WaitAsync(TimeSpan.FromSeconds(1)))
        {
            Assert.That(coordinator.SegmentGateCount, Is.EqualTo(1));
        }

        Assert.That(coordinator.SegmentGateCount, Is.Zero);
    }

    [Test]
    public async Task EnterSegmentAsync_CancelledWaiterDoesNotPoisonKey()
    {
        HlsTranscodeCoordinator coordinator = CreateCoordinator(maxConcurrentTranscodes: 1);
        using CancellationTokenSource cancellation = new();

        await using (IAsyncDisposable first =
            await coordinator.EnterSegmentAsync("segment-a", CancellationToken.None))
        {
            Task<IAsyncDisposable> cancelled = coordinator
                .EnterSegmentAsync("segment-a", cancellation.Token)
                .AsTask();
            cancellation.Cancel();

            Assert.CatchAsync<OperationCanceledException>(
                async () => await cancelled.WaitAsync(TimeSpan.FromSeconds(1)));
            Assert.That(coordinator.SegmentGateCount, Is.EqualTo(1));
        }

        await using (IAsyncDisposable admitted =
            await coordinator.EnterSegmentAsync("segment-a", CancellationToken.None))
        {
            Assert.That(coordinator.SegmentGateCount, Is.EqualTo(1));
        }

        Assert.That(coordinator.SegmentGateCount, Is.Zero);
    }

    private static HlsTranscodeCoordinator CreateCoordinator(int maxConcurrentTranscodes)
    {
        ResourceConcurrencyOptions options = new()
        {
            HlsTranscodes = maxConcurrentTranscodes,
        };
        return new HlsTranscodeCoordinator(Options.Create(options));
    }
}
