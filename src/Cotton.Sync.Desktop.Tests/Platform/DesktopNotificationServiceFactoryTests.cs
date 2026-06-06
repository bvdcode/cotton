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
            _tempDirectory,
            dbusSessionBusAddress: "unix:path=/tmp/cotton-test-bus");

        Assert.That(service, Is.TypeOf<NotifySendNotificationService>());
    }

    [Test]
    public void CreateCapabilitySnapshot_DescribesLinuxNotifySendIdentity()
    {
        string commandPath = Path.Combine(_tempDirectory, "notify-send");
        File.WriteAllText(commandPath, string.Empty);
        string iconPath = CreatePackagedIcon();

        DesktopNotificationCapabilitySnapshot snapshot = DesktopNotificationServiceFactory.CreateCapabilitySnapshot(
            DesktopNotificationPlatform.Linux,
            _tempDirectory,
            _tempDirectory,
            dbusSessionBusAddress: "unix:path=/tmp/cotton-test-bus");

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Platform, Is.EqualTo(DesktopNotificationPlatform.Linux));
            Assert.That(snapshot.IsSupported, Is.True);
            Assert.That(snapshot.AdapterName, Is.EqualTo("notify-send"));
            Assert.That(snapshot.AppName, Is.EqualTo("Cotton Sync"));
            Assert.That(snapshot.AppUserModelId, Is.Null);
            Assert.That(snapshot.ExecutablePath, Is.EqualTo(commandPath));
            Assert.That(snapshot.IconPath, Is.EqualTo(iconPath));
            Assert.That(snapshot.Details, Does.Contain("adapter: notify-send"));
            Assert.That(snapshot.Details, Does.Contain("app name: Cotton Sync"));
            Assert.That(snapshot.Details, Does.Contain("icon: " + iconPath));
            Assert.That(snapshot.Details, Does.Contain("session bus: available"));
            Assert.That(snapshot.Details, Does.Contain("sender name, icon rendering, timeout, and actions depend on the desktop notification daemon"));
            Assert.That(snapshot.Details, Does.Contain("actions are not used"));
        });
    }

    [Test]
    public void CreateCapabilitySnapshot_ReportsLinuxNotifySendUnavailableWithoutSessionBus()
    {
        string commandPath = Path.Combine(_tempDirectory, "notify-send");
        File.WriteAllText(commandPath, string.Empty);

        DesktopNotificationCapabilitySnapshot snapshot = DesktopNotificationServiceFactory.CreateCapabilitySnapshot(
            DesktopNotificationPlatform.Linux,
            _tempDirectory,
            _tempDirectory,
            dbusSessionBusAddress: null);
        IDesktopNotificationService service = DesktopNotificationServiceFactory.CreateForPlatform(
            DesktopNotificationPlatform.Linux,
            _tempDirectory,
            dbusSessionBusAddress: null);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.IsSupported, Is.False);
            Assert.That(snapshot.ExecutablePath, Is.EqualTo(commandPath));
            Assert.That(snapshot.Details, Does.StartWith("Not available on this platform"));
            Assert.That(snapshot.Details, Does.Contain("session bus: missing"));
            Assert.That(service, Is.TypeOf<UnsupportedDesktopNotificationService>());
        });
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
    public void CreateCapabilitySnapshot_DescribesWindowsToastIdentity()
    {
        string commandPath = Path.Combine(_tempDirectory, "powershell.exe");
        File.WriteAllText(commandPath, string.Empty);
        string iconPath = CreatePackagedIcon();

        DesktopNotificationCapabilitySnapshot snapshot = DesktopNotificationServiceFactory.CreateCapabilitySnapshot(
            DesktopNotificationPlatform.Windows,
            _tempDirectory,
            _tempDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Platform, Is.EqualTo(DesktopNotificationPlatform.Windows));
            Assert.That(snapshot.IsSupported, Is.True);
            Assert.That(snapshot.AdapterName, Is.EqualTo("Windows toast"));
            Assert.That(snapshot.AppName, Is.EqualTo("Cotton Sync"));
            Assert.That(snapshot.AppUserModelId, Is.EqualTo(DesktopAppIdentity.AppUserModelId));
            Assert.That(snapshot.ExecutablePath, Is.EqualTo(commandPath));
            Assert.That(snapshot.IconPath, Is.EqualTo(iconPath));
            Assert.That(snapshot.Details, Does.Contain("adapter: Windows toast"));
            Assert.That(snapshot.Details, Does.Contain("AppUserModelID: " + DesktopAppIdentity.AppUserModelId));
            Assert.That(snapshot.Details, Does.Contain("icon: " + iconPath));
            Assert.That(snapshot.Details, Does.Contain("registered Start Menu AppUserModelID shortcut"));
        });
    }

    [Test]
    public void CreateForPlatform_ReturnsUnsupportedWhenPlatformExecutableIsMissing()
    {
        IDesktopNotificationService service = DesktopNotificationServiceFactory.CreateForPlatform(
            DesktopNotificationPlatform.Windows,
            _tempDirectory);

        Assert.That(service, Is.TypeOf<UnsupportedDesktopNotificationService>());
    }

    [Test]
    public void CreateCapabilitySnapshot_ReportsMissingPlatformExecutable()
    {
        DesktopNotificationCapabilitySnapshot snapshot = DesktopNotificationServiceFactory.CreateCapabilitySnapshot(
            DesktopNotificationPlatform.Windows,
            _tempDirectory,
            _tempDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.IsSupported, Is.False);
            Assert.That(snapshot.ExecutablePath, Is.Null);
            Assert.That(snapshot.Details, Does.StartWith("Not available on this platform"));
            Assert.That(snapshot.Details, Does.Contain("AppUserModelID: " + DesktopAppIdentity.AppUserModelId));
            Assert.That(snapshot.Details, Does.Contain("icon: missing"));
        });
    }

    private string CreatePackagedIcon()
    {
        string assetsDirectory = Path.Combine(_tempDirectory, "Assets");
        Directory.CreateDirectory(assetsDirectory);
        string iconPath = Path.Combine(assetsDirectory, "icon-192.png");
        File.WriteAllBytes(iconPath, [1, 2, 3]);
        return iconPath;
    }
}
