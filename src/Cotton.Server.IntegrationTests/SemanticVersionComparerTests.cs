// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Helpers;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests;

public class SemanticVersionComparerTests
{
    [Test]
    public void IsNewer_TreatsDevMinorAsNewerThanOlderStableRelease()
    {
        Assert.That(SemanticVersionComparer.IsNewer("0.4.1", "0.5.0-alpha.58"), Is.False);
    }

    [Test]
    public void IsNewer_TreatsStableReleaseAsNewerThanSamePrerelease()
    {
        Assert.That(SemanticVersionComparer.IsNewer("v0.4.1", "0.4.1-alpha.58"), Is.True);
    }

    [Test]
    public void IsDowngrade_DetectsLowerCurrentVersion()
    {
        Assert.That(SemanticVersionComparer.IsDowngrade("0.4.0", "0.4.1"), Is.True);
    }

    [Test]
    public void IsGreaterThanOrEqual_TreatsPrereleaseAsLowerThanStableTarget()
    {
        Assert.That(SemanticVersionComparer.IsGreaterThanOrEqual("0.5.0-alpha.482", "0.5.0"), Is.False);
    }

    [Test]
    public void IsLessThan_DetectsStableUpperBound()
    {
        Assert.That(SemanticVersionComparer.IsLessThan("0.4.33", "0.5.0"), Is.True);
    }
}
