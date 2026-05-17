// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests;

public class HlsSegmentCacheTests
{
    [Test]
    public void Set_StoresSegmentUnderContentAddressedKey()
    {
        using var cache = CreateCache(sizeLimitBytes: 1024);
        string key = HlsSegmentCache.BuildKey(Guid.NewGuid(), "source", 2);
        byte[] bytes = [1, 2, 3, 4];

        cache.Set(key, bytes);

        Assert.Multiple(() =>
        {
            Assert.That(cache.TryGet(key, out byte[]? cached), Is.True);
            Assert.That(cached, Is.EqualTo(bytes));
        });
    }

    [Test]
    public void Set_SkipsEntriesLargerThanCacheLimit()
    {
        using var cache = CreateCache(sizeLimitBytes: 3);
        string key = HlsSegmentCache.BuildKey(Guid.NewGuid(), "low", 0);

        cache.Set(key, [1, 2, 3, 4]);

        Assert.That(cache.TryGet(key, out _), Is.False);
    }

    [Test]
    public void BuildKey_SeparatesManifestQualityAndSegment()
    {
        Guid manifestId = Guid.NewGuid();

        Assert.Multiple(() =>
        {
            Assert.That(
                HlsSegmentCache.BuildKey(manifestId, "source", 1),
                Is.Not.EqualTo(HlsSegmentCache.BuildKey(manifestId, "source", 2)));
            Assert.That(
                HlsSegmentCache.BuildKey(manifestId, "source", 1),
                Is.Not.EqualTo(HlsSegmentCache.BuildKey(manifestId, "low", 1)));
        });
    }

    private static HlsSegmentCache CreateCache(long sizeLimitBytes) =>
        new(Options.Create(new HlsSegmentCacheOptions
        {
            SizeLimitBytes = sizeLimitBytes,
        }));
}
