// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Desktop.Platform;

namespace Cotton.Sync.Desktop.Tests.Platform
{
    public class DesktopPlatformCapabilitiesTests
    {
        [Test]
        public void CreateSnapshot_MatchesPlatformCapabilityFlags()
        {
            DesktopPlatformCapabilitySnapshot snapshot = DesktopPlatformCapabilities.CreateSnapshot();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.OperatingSystemName, Is.Not.Empty);
                Assert.That(snapshot.DesktopSession, Is.Not.Empty);
                Assert.That(snapshot.CurrentDesktop, Is.Not.Empty);
                Assert.That(snapshot.IsAutostartSupported, Is.EqualTo(DesktopPlatformCapabilities.IsAutostartSupported));
                Assert.That(snapshot.IsTrayLifecycleSupported, Is.EqualTo(DesktopPlatformCapabilities.IsTrayLifecycleSupported));
                Assert.That(snapshot.TrayLifecycleDetails, Is.Not.Empty);
            });
        }

        [Test]
        public void CreateSnapshot_OnLinux_DoesNotClaimTrayLifecycleSupport()
        {
            if (!OperatingSystem.IsLinux())
            {
                Assert.Pass("Linux tray lifecycle guard is only evaluated on Linux.");
            }

            DesktopPlatformCapabilitySnapshot snapshot = DesktopPlatformCapabilities.CreateSnapshot();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.OperatingSystemName, Is.EqualTo("Linux"));
                Assert.That(snapshot.IsTrayLifecycleSupported, Is.False);
                Assert.That(snapshot.TrayLifecycleDetails, Does.Contain("varies by desktop environment"));
            });
        }
    }
}
