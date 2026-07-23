// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Previews;
using Cotton.Server.Services;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests;

public class HlsMediaProbeCachePolicyTests
{
    [Test]
    public void GetLifetime_UnavailableProbe_UsesShortRetryWindow()
    {
        TimeSpan lifetime = HlsMediaProbeCachePolicy.GetLifetime(null);

        Assert.That(lifetime, Is.EqualTo(TimeSpan.FromSeconds(10)));
    }

    [Test]
    public void GetLifetime_SuccessfulProbe_PreservesLongCache()
    {
        MediaProbeInfo probe = new(42, "h264", "aac");

        TimeSpan lifetime = HlsMediaProbeCachePolicy.GetLifetime(probe);

        Assert.That(lifetime, Is.EqualTo(TimeSpan.FromHours(1)));
    }
}
