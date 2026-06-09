// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Desktop.ViewModels;

namespace Cotton.Sync.Desktop.Tests.ViewModels
{
    public class DesktopFeatureFlagsTests
    {
        [TestCase("1")]
        [TestCase("true")]
        [TestCase("TRUE")]
        [TestCase("yes")]
        public void FromEnvironment_EnablesFutureSyncModesForTruthyValues(string value)
        {
            DesktopFeatureFlags flags = DesktopFeatureFlags.FromEnvironment(_ => value);

            Assert.That(flags.ShowFutureSyncModes, Is.True);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("0")]
        [TestCase("false")]
        [TestCase("no")]
        public void FromEnvironment_DisablesFutureSyncModesForOtherValues(string? value)
        {
            DesktopFeatureFlags flags = DesktopFeatureFlags.FromEnvironment(_ => value);

            Assert.That(flags.ShowFutureSyncModes, Is.False);
        }
    }
}
