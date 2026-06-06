// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Desktop.Platform;

namespace Cotton.Sync.Desktop.Tests.Platform;

public sealed class DesktopNotificationServiceFactoryTests
{
    private string _tempDirectory = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "cotton-notify-path-" + Guid.NewGuid().ToString("N"));
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
    public void ResolveExecutablePath_ReturnsCommandFromPath()
    {
        string commandPath = Path.Combine(_tempDirectory, "notify-send");
        File.WriteAllText(commandPath, string.Empty);

        string? result = DesktopNotificationServiceFactory.ResolveExecutablePath("notify-send", _tempDirectory);

        Assert.That(result, Is.EqualTo(commandPath));
    }

    [Test]
    public void ResolveExecutablePath_ReturnsNullWhenCommandIsMissing()
    {
        string? result = DesktopNotificationServiceFactory.ResolveExecutablePath("notify-send", _tempDirectory);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void ResolveNotificationIconPath_ReturnsPackagedIconWhenPresent()
    {
        string assetsDirectory = Path.Combine(_tempDirectory, "Assets");
        Directory.CreateDirectory(assetsDirectory);
        string iconPath = Path.Combine(assetsDirectory, "icon-192.png");
        File.WriteAllBytes(iconPath, [1, 2, 3]);

        string? result = DesktopNotificationServiceFactory.ResolveNotificationIconPath(_tempDirectory);

        Assert.That(result, Is.EqualTo(iconPath));
    }

    [Test]
    public void ResolveNotificationIconPath_ReturnsNullWhenPackagedIconIsMissing()
    {
        string? result = DesktopNotificationServiceFactory.ResolveNotificationIconPath(_tempDirectory);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void CreateForPlatform_ReturnsLinuxNotifySendAdapterWhenNotifySendExists()
    {
        string commandPath = Path.Combine(_tempDirectory, "notify-send");
        File.WriteAllText(commandPath, string.Empty);

        IDesktopNotificationService service = DesktopNotificationServiceFactory.CreateForPlatform(
            DesktopNotificationPlatform.Linux,
            _tempDirectory);

        Assert.That(service, Is.TypeOf<NotifySendNotificationService>());
    }

    [Test]
    public void CreateForPlatform_ReturnsWindowsToastAdapterWhenPowerShellExists()
    {
        string commandPath = Path.Combine(_tempDirectory, "powershell.exe");
        File.WriteAllText(commandPath, string.Empty);

        IDesktopNotificationService service = DesktopNotificationServiceFactory.CreateForPlatform(
            DesktopNotificationPlatform.Windows,
            _tempDirectory);

        Assert.That(service, Is.TypeOf<WindowsToastNotificationService>());
    }

    [Test]
    public void CreateForPlatform_ReturnsUnsupportedWhenPlatformExecutableIsMissing()
    {
        IDesktopNotificationService service = DesktopNotificationServiceFactory.CreateForPlatform(
            DesktopNotificationPlatform.Windows,
            _tempDirectory);

        Assert.That(service, Is.TypeOf<UnsupportedDesktopNotificationService>());
    }
}
