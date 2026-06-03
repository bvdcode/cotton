// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Desktop.Platform;

namespace Cotton.Sync.Desktop.Tests.Platform;

public sealed class XdgAutostartServiceTests
{
    private string _tempDirectory = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "cotton-xdg-autostart-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Test]
    public async Task SetEnabledAsync_WritesDesktopEntry()
    {
        var service = new XdgAutostartService(
            _tempDirectory,
            new AutostartLaunchCommand(
                "/opt/Cotton Sync/Cotton.Sync.Desktop",
                ["--start-minimized"]),
            "/opt/Cotton Sync/icon-192.png");

        await service.SetEnabledAsync(true);

        string desktopFilePath = Path.Combine(_tempDirectory, "cotton-sync.desktop");
        string content = await File.ReadAllTextAsync(desktopFilePath);
        bool isEnabled = await service.IsEnabledAsync();

        Assert.Multiple(() =>
        {
            Assert.That(isEnabled, Is.True);
            Assert.That(content, Does.Contain("Type=Application"));
            Assert.That(content, Does.Contain("Name=Cotton Sync"));
            Assert.That(
                content,
                Does.Contain("Exec=\"/opt/Cotton Sync/Cotton.Sync.Desktop\" --start-minimized"));
            Assert.That(content, Does.Contain("Icon=/opt/Cotton Sync/icon-192.png"));
            Assert.That(content, Does.Contain("X-GNOME-Autostart-enabled=true"));
        });
    }

    [Test]
    public async Task SetEnabledAsync_Disabled_RemovesDesktopEntry()
    {
        var service = new XdgAutostartService(
            _tempDirectory,
            new AutostartLaunchCommand("/opt/cotton/Cotton.Sync.Desktop", ["--start-minimized"]));
        await service.SetEnabledAsync(true);

        await service.SetEnabledAsync(false);
        bool isEnabled = await service.IsEnabledAsync();

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(Path.Combine(_tempDirectory, "cotton-sync.desktop")), Is.False);
            Assert.That(isEnabled, Is.False);
        });
    }
}
