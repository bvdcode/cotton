// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Avalonia.Styling;
using Cotton.Sync.App.Preferences;
using Cotton.Sync.Desktop.Platform;

namespace Cotton.Sync.Desktop.Tests.Platform
{

    public sealed class AvaloniaDesktopThemeServiceTests
    {
        [TestCase(AppThemeMode.System, "Default")]
        [TestCase(AppThemeMode.Light, "Light")]
        [TestCase(AppThemeMode.Dark, "Dark")]
        public void ToThemeVariant_MapsSupportedThemeModes(AppThemeMode mode, string expectedKey)
        {
            ThemeVariant variant = AvaloniaDesktopThemeService.ToThemeVariant(mode);

            Assert.That(variant.Key, Is.EqualTo(expectedKey));
        }

        [Test]
        public void ToThemeVariant_RejectsUnsupportedThemeMode()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => AvaloniaDesktopThemeService.ToThemeVariant((AppThemeMode)99));
        }
    }
}
