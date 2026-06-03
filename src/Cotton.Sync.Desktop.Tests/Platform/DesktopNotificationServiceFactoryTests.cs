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
}
