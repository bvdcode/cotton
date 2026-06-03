// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Desktop.Startup;

namespace Cotton.Sync.Desktop.Tests.Startup;

public sealed class DesktopStartupOptionsTests
{
    [Test]
    public void Parse_LoadsServerUrlAndUsername()
    {
        DesktopStartupOptions options = DesktopStartupOptions.Parse(
            [
                "--server-url",
                "https://app.cottoncloud.dev/",
                "--username=desktop@example.test",
            ]);

        Assert.Multiple(() =>
        {
            Assert.That(options.ServerUrl, Is.EqualTo(new Uri("https://app.cottoncloud.dev/")));
            Assert.That(options.Username, Is.EqualTo("desktop@example.test"));
            Assert.That(options.StartMinimizedToTray, Is.False);
        });
    }

    [Test]
    public void Parse_IgnoresUnsupportedServerUrl()
    {
        DesktopStartupOptions options = DesktopStartupOptions.Parse(
            [
                "--server-url",
                "file:///tmp/cotton",
                "--username",
                " desktop@example.test ",
            ]);

        Assert.Multiple(() =>
        {
            Assert.That(options.ServerUrl, Is.Null);
            Assert.That(options.Username, Is.EqualTo("desktop@example.test"));
        });
    }

    [Test]
    public void Parse_LoadsStartMinimizedFlag()
    {
        DesktopStartupOptions options = DesktopStartupOptions.Parse(
            [
                "--start-minimized",
            ]);

        Assert.That(options.StartMinimizedToTray, Is.True);
    }
}
