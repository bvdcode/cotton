// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Tests.Shell;

public sealed class DesktopTrayMenuContractTests
{
    [Test]
    public void TrayMenu_UsesDeterministicFolderAndCloudLabels()
    {
        string trayController = File.ReadAllText(GetDesktopShellFilePath("DesktopTrayController.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(trayController, Does.Contain("\"Open local folder\""));
            Assert.That(trayController, Does.Contain("\"Open in Cotton Cloud\""));
            Assert.That(trayController, Does.Contain("TrayOpenFolderLabel"));
            Assert.That(trayController, Does.Contain("OpenTrayFolderCommand"));
            Assert.That(trayController, Does.Not.Contain("\"Open selected folder\""));
            Assert.That(trayController, Does.Not.Contain("\"Open folder\""));
            Assert.That(trayController, Does.Not.Contain("\"Open Cotton Cloud\""));
            Assert.That(trayController, Does.Not.Contain("\"Open web\""));
        });
    }

    [Test]
    public void TrayMenu_HidesUnavailableActions()
    {
        string trayController = File.ReadAllText(GetDesktopShellFilePath("DesktopTrayController.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(trayController, Does.Contain("_openFolderMenuItem"));
            Assert.That(trayController, Does.Contain("_openWebMenuItem"));
            Assert.That(trayController, Does.Contain("_syncNowMenuItem"));
            Assert.That(trayController, Does.Contain("_pauseResumeMenuItem"));
            Assert.That(trayController, Does.Contain("_settingsMenuItem"));
            Assert.That(trayController, Does.Contain("SetMenuItemAvailability(_openFolderMenuItem"));
            Assert.That(trayController, Does.Contain("SetMenuItemAvailability(_openWebMenuItem"));
            Assert.That(trayController, Does.Contain("SetMenuItemAvailability(_syncNowMenuItem"));
            Assert.That(trayController, Does.Contain("SetMenuItemAvailability(_settingsMenuItem"));
            Assert.That(trayController, Does.Contain("SetMenuItemAvailability(_pauseResumeMenuItem"));
            Assert.That(trayController, Does.Contain("menuItem.IsVisible = isAvailable"));
            Assert.That(trayController, Does.Contain("nameof(ShellViewModel.CanOpenTrayFolder)"));
            Assert.That(trayController, Does.Contain("nameof(ShellViewModel.TrayOpenFolderLabel)"));
            Assert.That(trayController, Does.Contain("nameof(ShellViewModel.HasCurrentWorkProgress)"));
            Assert.That(trayController, Does.Not.Contain("SetMenuItemEnabled"));
            Assert.That(trayController, Does.Not.Contain("SetMenuItemEnabled(_openFolderMenuItem"));
            Assert.That(trayController, Does.Not.Contain("nameof(ShellViewModel.SelectedSyncPair)"));
        });
    }

    private static string GetDesktopShellFilePath(string fileName)
    {
        string directory = TestContext.CurrentContext.TestDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            string candidate = Path.Combine(directory, "src", "Cotton.Sync.Desktop", "Shell", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            string? parent = Directory.GetParent(directory)?.FullName;
            if (parent == directory)
            {
                break;
            }

            directory = parent ?? string.Empty;
        }

        throw new FileNotFoundException(fileName + " was not found from the test directory.");
    }
}
