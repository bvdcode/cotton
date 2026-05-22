// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Server.IntegrationTests.Helpers;
using Cotton.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests;

public sealed class StoragePipelineProbeServiceTests
{
    [Test]
    public async Task RunAsync_UsesWarmupThenMeasuredIteration_AndDeletesTemporaryBlobs()
    {
        var storage = new InMemoryStorage();
        var service = new StoragePipelineProbeService(
            storage,
            NullLogger<StoragePipelineProbeService>.Instance);

        var result = await service.RunAsync(CancellationToken.None);

        var keys = new List<string>();
        await foreach (string key in storage.ListAllKeysAsync())
        {
            keys.Add(key);
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.PayloadSizeBytes, Is.EqualTo(StoragePipelineProbeService.PayloadSizeBytes));
            Assert.That(result.CompletedAtUtc, Is.Not.EqualTo(default(DateTimeOffset)));
            Assert.That(result.Warmup.IsWarmup, Is.True);
            Assert.That(result.Measured.IsWarmup, Is.False);
            Assert.That(result.Warmup.StoredSizeBytes, Is.EqualTo(StoragePipelineProbeService.PayloadSizeBytes));
            Assert.That(result.Measured.StoredSizeBytes, Is.EqualTo(StoragePipelineProbeService.PayloadSizeBytes));
            Assert.That(result.Measured.WriteMebibytesPerSecond, Is.GreaterThan(0));
            Assert.That(result.Measured.ReadMebibytesPerSecond, Is.GreaterThan(0));
            Assert.That(keys, Is.Empty);
        }
    }
}
