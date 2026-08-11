// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Helpers;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests;

public class SemanticVersionComparerTests
{
    [TestCase("0.5.1", "0.5.0", true)]
    [TestCase("0.4.1", "0.5.0-alpha.58", false)]
    [TestCase("0.5.0-alpha.713", "0.5.0-alpha.712", true)]
    [TestCase("0.5.0-alpha.10", "0.5.0-alpha.9", true)]
    [TestCase("0.5.0-beta", "0.5.0-alpha.999", true)]
    [TestCase("0.5.0-alpha", "0.5.0-alpha.1", false)]
    [TestCase("0.5.0", "0.5.0-rc.1", true)]
    [TestCase("0.5.0+build.2", "0.5.0+build.1", false)]
    public void IsNewer_UsesSemanticVersionPrecedence(
        string candidateVersion,
        string currentVersion,
        bool expected)
    {
        Assert.That(
            SemanticVersionComparer.IsNewer(candidateVersion, currentVersion),
            Is.EqualTo(expected));
    }

    [TestCase("v0.4.1", "0.4.1-alpha.58", true)]
    [TestCase(" V0.4.1 ", "v0.4.1", false)]
    public void IsNewer_AcceptsGitTagPrefix(
        string candidateVersion,
        string currentVersion,
        bool expected)
    {
        Assert.That(
            SemanticVersionComparer.IsNewer(candidateVersion, currentVersion),
            Is.EqualTo(expected));
    }

    [TestCase("", "0.5.0")]
    [TestCase("not-a-version", "0.5.0")]
    [TestCase("0.5.0", "not-a-version")]
    public void IsNewer_ReturnsFalseForUnparseableInput(
        string candidateVersion,
        string currentVersion)
    {
        Assert.That(
            SemanticVersionComparer.IsNewer(candidateVersion, currentVersion),
            Is.False);
    }

    [TestCase("0.4.0", "0.4.1", true)]
    [TestCase("0.5.0-alpha.712", "0.5.0-alpha.713", true)]
    [TestCase("0.5.0", "0.5.0", false)]
    [TestCase("0.5.1", "0.5.0", false)]
    [TestCase("not-a-version", "0.5.0", false)]
    public void IsDowngrade_UsesSemanticVersionPrecedence(
        string currentVersion,
        string latestVersion,
        bool expected)
    {
        Assert.That(
            SemanticVersionComparer.IsDowngrade(currentVersion, latestVersion),
            Is.EqualTo(expected));
    }
}
