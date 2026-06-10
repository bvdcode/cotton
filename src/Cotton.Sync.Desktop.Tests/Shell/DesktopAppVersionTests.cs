// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Desktop.Shell;

namespace Cotton.Sync.Desktop.Tests.Shell
{
    public class DesktopAppVersionTests
    {
        [Test]
        public void CurrentDoesNotExposeBuildMetadata()
        {
            Assert.That(DesktopAppVersion.Current, Does.Not.Contain("+"));
            Assert.That(DesktopAppVersion.Current, Is.Not.EqualTo("unknown"));
        }
    }
}
