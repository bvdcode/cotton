// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Desktop.Shell;

namespace Cotton.Sync.Desktop.Tests.Shell;

public sealed class DesktopWindowLifecyclePolicyTests
{
    [Test]
    public void ResolveCloseAction_HidesToTrayWhenTrayLifecycleIsAvailable()
    {
        var policy = new DesktopWindowLifecyclePolicy(
            hideAfterSessionRestore: false,
            canHideToTray: true);

        DesktopWindowCloseAction action = policy.ResolveCloseAction();

        Assert.That(action, Is.EqualTo(DesktopWindowCloseAction.HideToTray));
    }

    [Test]
    public void ResolveCloseAction_ClosesWhenTrayLifecycleIsUnavailable()
    {
        var policy = new DesktopWindowLifecyclePolicy(
            hideAfterSessionRestore: false,
            canHideToTray: false);

        DesktopWindowCloseAction action = policy.ResolveCloseAction();

        Assert.That(action, Is.EqualTo(DesktopWindowCloseAction.Close));
    }

    [Test]
    public void ResolveCloseAction_ClosesAfterExplicitQuitRequest()
    {
        var policy = new DesktopWindowLifecyclePolicy(
            hideAfterSessionRestore: false,
            canHideToTray: true);

        policy.RequestQuit();
        DesktopWindowCloseAction action = policy.ResolveCloseAction();

        Assert.That(action, Is.EqualTo(DesktopWindowCloseAction.Close));
    }

    [Test]
    public void ShouldHideAfterSessionRestore_RequiresTrayLifecycleAndRestoredDashboard()
    {
        var supportedPolicy = new DesktopWindowLifecyclePolicy(
            hideAfterSessionRestore: true,
            canHideToTray: true);
        var unsupportedPolicy = new DesktopWindowLifecyclePolicy(
            hideAfterSessionRestore: true,
            canHideToTray: false);

        Assert.Multiple(() =>
        {
            Assert.That(supportedPolicy.ShouldHideAfterSessionRestore(isDashboardVisible: true), Is.True);
            Assert.That(supportedPolicy.ShouldHideAfterSessionRestore(isDashboardVisible: false), Is.False);
            Assert.That(unsupportedPolicy.ShouldHideAfterSessionRestore(isDashboardVisible: true), Is.False);
        });
    }
}
