// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Desktop.Platform;

namespace Cotton.Sync.Desktop.Tests.Platform;

public sealed class WindowsRunAutostartServiceTests
{
    [Test]
    public void TryCreateDefaultLaunchCommand_ReturnsNullForDotnetHost()
    {
        AutostartLaunchCommand? command = AutostartLaunchCommand.TryCreate(
            @"C:\Program Files\dotnet\dotnet.exe",
            [@"C:\repo\cotton\src\Cotton.Sync.Desktop\bin\Debug\net10.0\Cotton.Sync.Desktop.dll"],
            @"C:\repo\cotton\src\Cotton.Sync.Desktop\bin\Debug\net10.0",
            startMinimized: true);

        Assert.That(command, Is.Null);
    }

    [Test]
    public void TryCreateDefaultLaunchCommand_ReturnsNullForDevelopmentBuildOutput()
    {
        AutostartLaunchCommand? command = AutostartLaunchCommand.TryCreate(
            @"C:\repo\cotton\src\Cotton.Sync.Desktop\bin\Debug\net10.0\Cotton.Sync.Desktop.exe",
            [@"C:\repo\cotton\src\Cotton.Sync.Desktop\bin\Debug\net10.0\Cotton.Sync.Desktop.exe"],
            @"C:\repo\cotton\src\Cotton.Sync.Desktop\bin\Debug\net10.0",
            startMinimized: true);

        Assert.That(command, Is.Null);
    }

    [Test]
    public void TryCreateDefaultLaunchCommand_AllowsPublishedApphost()
    {
        AutostartLaunchCommand? command = AutostartLaunchCommand.TryCreate(
            @"C:\repo\cotton\src\Cotton.Sync.Desktop\bin\Release\net10.0\publish\win-x64\Cotton.Sync.Desktop.exe",
            [@"C:\repo\cotton\src\Cotton.Sync.Desktop\bin\Release\net10.0\publish\win-x64\Cotton.Sync.Desktop.exe"],
            @"C:\repo\cotton\src\Cotton.Sync.Desktop\bin\Release\net10.0\publish\win-x64",
            startMinimized: true);

        Assert.Multiple(() =>
        {
            Assert.That(command, Is.Not.Null);
            Assert.That(command!.Arguments, Is.EqualTo(new[] { "--start-minimized" }));
            Assert.That(command.ExecutablePath, Does.EndWith(@"Cotton.Sync.Desktop.exe"));
        });
    }

    [Test]
    public void TryCreateDefaultLaunchCommand_AllowsInstalledApphost()
    {
        AutostartLaunchCommand? command = AutostartLaunchCommand.TryCreate(
            @"C:\Users\qa\AppData\Local\Programs\Cotton Sync\Cotton.Sync.Desktop.exe",
            [@"C:\Users\qa\AppData\Local\Programs\Cotton Sync\Cotton.Sync.Desktop.exe"],
            @"C:\Users\qa\AppData\Local\Programs\Cotton Sync",
            startMinimized: true);

        Assert.Multiple(() =>
        {
            Assert.That(command, Is.Not.Null);
            Assert.That(command!.ToString(), Does.Contain("--start-minimized"));
            Assert.That(command.ToString(), Does.Contain("Cotton Sync"));
        });
    }

    [Test]
    public async Task SetEnabledAsync_WritesLaunchCommandToRunRegistry()
    {
        var registry = new FakeWindowsRunRegistry();
        var command = new AutostartLaunchCommand(
            @"C:\Program Files\Cotton\Cotton.Sync.Desktop.exe",
            ["--start-minimized"]);
        var service = new WindowsRunAutostartService(command, registry);

        await service.SetEnabledAsync(true);

        Assert.Multiple(() =>
        {
            Assert.That(service.IsSupported, Is.True);
            Assert.That(registry.Values, Has.Count.EqualTo(1));
            Assert.That(registry.Values["Cotton Sync"], Is.EqualTo(command.ToString()));
        });
    }

    [Test]
    public async Task IsEnabledAsync_ReturnsTrueOnlyForMatchingLaunchCommand()
    {
        var command = new AutostartLaunchCommand(
            @"C:\Cotton\Cotton.Sync.Desktop.exe",
            ["--start-minimized"]);
        var registry = new FakeWindowsRunRegistry
        {
            Values =
            {
                ["Cotton Sync"] = command.ToString(),
            },
        };
        var service = new WindowsRunAutostartService(command, registry);

        bool isEnabled = await service.IsEnabledAsync();

        registry.Values["Cotton Sync"] = "\"C:\\Other\\Cotton.Sync.Desktop.exe\"";
        bool wrongCommandIsEnabled = await service.IsEnabledAsync();

        Assert.Multiple(() =>
        {
            Assert.That(isEnabled, Is.True);
            Assert.That(wrongCommandIsEnabled, Is.False);
        });
    }

    [Test]
    public async Task SetEnabledAsync_RemovesRunRegistryValueWhenDisabled()
    {
        var command = new AutostartLaunchCommand(
            @"C:\Cotton\Cotton.Sync.Desktop.exe",
            ["--start-minimized"]);
        var registry = new FakeWindowsRunRegistry
        {
            Values =
            {
                ["Cotton Sync"] = command.ToString(),
            },
        };
        var service = new WindowsRunAutostartService(command, registry);

        await service.SetEnabledAsync(false);

        Assert.That(registry.Values, Does.Not.ContainKey("Cotton Sync"));
    }

    private sealed class FakeWindowsRunRegistry : IWindowsRunRegistry
    {
        public Dictionary<string, string> Values { get; } = [];

        public string? GetValue(string valueName)
        {
            return Values.GetValueOrDefault(valueName);
        }

        public void SetValue(string valueName, string value)
        {
            Values[valueName] = value;
        }

        public void DeleteValue(string valueName)
        {
            Values.Remove(valueName);
        }
    }
}
