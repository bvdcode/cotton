// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Desktop.Shell;

namespace Cotton.Sync.Desktop.Tests.Shell
{
    public class DesktopWindowLifecyclePolicyTests
    {
        [Test]
        public void ResolveCloseAction_HidesToTrayWhenTrayLifecycleIsAvailable()
        {
            var policy = new DesktopWindowLifecyclePolicy(
                startMinimizedToTray: false,
                canHideToTray: true);

            DesktopWindowCloseAction action = policy.ResolveCloseAction();

            Assert.That(action, Is.EqualTo(DesktopWindowCloseAction.HideToTray));
        }

        [Test]
        public void ResolveCloseAction_ClosesWhenTrayLifecycleIsUnavailable()
        {
            var policy = new DesktopWindowLifecyclePolicy(
                startMinimizedToTray: false,
                canHideToTray: false);

            DesktopWindowCloseAction action = policy.ResolveCloseAction();

            Assert.That(action, Is.EqualTo(DesktopWindowCloseAction.Close));
        }

        [Test]
        public void ResolveCloseAction_ClosesAfterExplicitQuitRequest()
        {
            var policy = new DesktopWindowLifecyclePolicy(
                startMinimizedToTray: false,
                canHideToTray: true);

            policy.RequestQuit();
            DesktopWindowCloseAction action = policy.ResolveCloseAction();

            Assert.That(action, Is.EqualTo(DesktopWindowCloseAction.Close));
        }

        [Test]
        public void ShouldHideAfterStartup_RequiresTrayLifecycleOnly()
        {
            var supportedPolicy = new DesktopWindowLifecyclePolicy(
                startMinimizedToTray: true,
                canHideToTray: true);
            var unsupportedPolicy = new DesktopWindowLifecyclePolicy(
                startMinimizedToTray: true,
                canHideToTray: false);
            var normalPolicy = new DesktopWindowLifecyclePolicy(
                startMinimizedToTray: false,
                canHideToTray: true);

            Assert.Multiple(() =>
            {
                Assert.That(supportedPolicy.ShouldHideAfterStartup(), Is.True);
                Assert.That(unsupportedPolicy.ShouldHideAfterStartup(), Is.False);
                Assert.That(normalPolicy.ShouldHideAfterStartup(), Is.False);
            });
        }

        [Test]
        public void ShouldHideAfterStartup_DoesNotHideAfterExplicitShowRequest()
        {
            var policy = new DesktopWindowLifecyclePolicy(
                startMinimizedToTray: true,
                canHideToTray: true);

            policy.RequestShow();

            Assert.That(policy.ShouldHideAfterStartup(), Is.False);
        }
    }
}
