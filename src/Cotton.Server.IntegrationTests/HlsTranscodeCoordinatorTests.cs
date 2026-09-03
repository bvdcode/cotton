// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Configuration;
using Cotton.Server.Services;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class HlsTranscodeCoordinatorTests
    {
        [Test]
        public void Constructor_RejectsNonPositiveProbeLimit()
        {
            ResourceConcurrencyOptions options = new()
            {
                HlsProbes = 0,
            };

            Assert.Throws<ArgumentOutOfRangeException>(
                () => _ = new HlsTranscodeCoordinator(Options.Create(options)));
        }

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
                await cancellation.CancelAsync();

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
                await cancellation.CancelAsync();

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

        [Test]
        public async Task EnterProbeAsync_QueuesAboveConfiguredLimit()
        {
            HlsTranscodeCoordinator coordinator = CreateCoordinator(maxConcurrentProbes: 2);
            Task<IAsyncDisposable> queued;

            await using (IAsyncDisposable first =
                await coordinator.EnterProbeAsync(CancellationToken.None))
            await using (IAsyncDisposable second =
                await coordinator.EnterProbeAsync(CancellationToken.None))
            {
                queued = coordinator.EnterProbeAsync(CancellationToken.None).AsTask();
                Assert.That(queued.IsCompleted, Is.False);
            }

            await using IAsyncDisposable admitted =
                await queued.WaitAsync(TimeSpan.FromSeconds(1));
        }

        [Test]
        public async Task EnterProbeAsync_CancelledWaiterDoesNotConsumePermit()
        {
            HlsTranscodeCoordinator coordinator = CreateCoordinator(maxConcurrentProbes: 1);
            using CancellationTokenSource cancellation = new();

            await using (IAsyncDisposable first =
                await coordinator.EnterProbeAsync(CancellationToken.None))
            {
                Task<IAsyncDisposable> cancelled = coordinator
                    .EnterProbeAsync(cancellation.Token)
                    .AsTask();
                await cancellation.CancelAsync();

                Assert.CatchAsync<OperationCanceledException>(
                    async () => await cancelled.WaitAsync(TimeSpan.FromSeconds(1)));
            }

            await using IAsyncDisposable admitted = await coordinator
                .EnterProbeAsync(CancellationToken.None)
                .AsTask()
                .WaitAsync(TimeSpan.FromSeconds(1));
        }

        [Test]
        public async Task EnterProbeManifestAsync_SerializesSameManifestWithoutBlockingOthers()
        {
            HlsTranscodeCoordinator coordinator = CreateCoordinator();
            Guid manifestId = Guid.NewGuid();
            Task<IAsyncDisposable> sameManifest;

            await using (IAsyncDisposable first =
                await coordinator.EnterProbeManifestAsync(manifestId, CancellationToken.None))
            {
                sameManifest = coordinator
                    .EnterProbeManifestAsync(manifestId, CancellationToken.None)
                    .AsTask();
                await using IAsyncDisposable otherManifest =
                    await coordinator.EnterProbeManifestAsync(Guid.NewGuid(), CancellationToken.None);

                Assert.Multiple(() =>
                {
                    Assert.That(sameManifest.IsCompleted, Is.False);
                    Assert.That(coordinator.ProbeManifestGateCount, Is.EqualTo(2));
                });
            }

            await using (IAsyncDisposable admitted =
                await sameManifest.WaitAsync(TimeSpan.FromSeconds(1)))
            {
                Assert.That(coordinator.ProbeManifestGateCount, Is.EqualTo(1));
            }

            Assert.That(coordinator.ProbeManifestGateCount, Is.Zero);
        }

        [Test]
        public async Task EnterProbeManifestAsync_CancelledWaiterDoesNotPoisonManifest()
        {
            HlsTranscodeCoordinator coordinator = CreateCoordinator();
            Guid manifestId = Guid.NewGuid();
            using CancellationTokenSource cancellation = new();

            await using (IAsyncDisposable first =
                await coordinator.EnterProbeManifestAsync(manifestId, CancellationToken.None))
            {
                Task<IAsyncDisposable> cancelled = coordinator
                    .EnterProbeManifestAsync(manifestId, cancellation.Token)
                    .AsTask();
                await cancellation.CancelAsync();

                Assert.CatchAsync<OperationCanceledException>(
                    async () => await cancelled.WaitAsync(TimeSpan.FromSeconds(1)));
                Assert.That(coordinator.ProbeManifestGateCount, Is.EqualTo(1));
            }

            await using (IAsyncDisposable admitted =
                await coordinator.EnterProbeManifestAsync(manifestId, CancellationToken.None))
            {
                Assert.That(coordinator.ProbeManifestGateCount, Is.EqualTo(1));
            }

            Assert.That(coordinator.ProbeManifestGateCount, Is.Zero);
        }

        private static HlsTranscodeCoordinator CreateCoordinator(
            int maxConcurrentTranscodes = 1,
            int maxConcurrentProbes = 1)
        {
            ResourceConcurrencyOptions options = new()
            {
                HlsTranscodes = maxConcurrentTranscodes,
                HlsProbes = maxConcurrentProbes,
            };
            return new HlsTranscodeCoordinator(Options.Create(options));
        }
    }
}
