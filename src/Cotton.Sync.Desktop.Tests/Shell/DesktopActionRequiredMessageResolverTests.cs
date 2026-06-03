// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Desktop.Shell;

namespace Cotton.Sync.Desktop.Tests.Shell;

public sealed class DesktopActionRequiredMessageResolverTests
{
    [Test]
    public void FromStatus_ReturnsFirstPairError()
    {
        var status = new DesktopSyncStatusSnapshot(
        [
            new DesktopSyncPairStatusSnapshot(Guid.NewGuid(), "Idle", null),
            new DesktopSyncPairStatusSnapshot(Guid.NewGuid(), "Error", "Remote folder is unavailable."),
            new DesktopSyncPairStatusSnapshot(Guid.NewGuid(), "Error", "Local folder is unavailable."),
        ]);

        string message = DesktopActionRequiredMessageResolver.FromStatus(status);

        Assert.That(message, Is.EqualTo("Remote folder is unavailable."));
    }

    [Test]
    public void FromStatus_ReturnsEmptyWhenNoPairHasError()
    {
        var status = new DesktopSyncStatusSnapshot(
        [
            new DesktopSyncPairStatusSnapshot(Guid.NewGuid(), "Idle", null),
            new DesktopSyncPairStatusSnapshot(Guid.NewGuid(), "Syncing", string.Empty),
        ]);

        string message = DesktopActionRequiredMessageResolver.FromStatus(status);

        Assert.That(message, Is.Empty);
    }

    [Test]
    public void FromSelfTest_ReturnsFirstFailedCheckDetails()
    {
        var result = new DesktopSelfTestSnapshot(
        [
            new DesktopSelfTestItemSnapshot("Database", true, "Ready"),
            new DesktopSelfTestItemSnapshot("Server", false, "Cotton server not found."),
            new DesktopSelfTestItemSnapshot("Local root", false, "Missing folder."),
        ]);

        string message = DesktopActionRequiredMessageResolver.FromSelfTest(result);

        Assert.That(message, Is.EqualTo("Cotton server not found."));
    }

    [Test]
    public void FromSelfTest_ReturnsEmptyWhenSelfTestPassed()
    {
        var result = new DesktopSelfTestSnapshot(
        [
            new DesktopSelfTestItemSnapshot("Database", true, "Ready"),
        ]);

        string message = DesktopActionRequiredMessageResolver.FromSelfTest(result);

        Assert.That(message, Is.Empty);
    }
}
