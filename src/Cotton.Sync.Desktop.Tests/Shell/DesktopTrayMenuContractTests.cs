// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Tests.Shell;

public sealed class DesktopTrayMenuContractTests
{
    [Test]
    public void TrayMenu_UsesSpecificFolderAndCloudLabels()
    {
        string trayController = File.ReadAllText(GetDesktopShellFilePath("DesktopTrayController.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(trayController, Does.Contain("\"Open selected folder\""));
            Assert.That(trayController, Does.Contain("\"Open Cotton Cloud\""));
            Assert.That(trayController, Does.Not.Contain("\"Open folder\""));
            Assert.That(trayController, Does.Not.Contain("\"Open web\""));
        });
    }

    [Test]
    public void TrayMenu_RefreshesEnabledStateForContextualActions()
    {
        string trayController = File.ReadAllText(GetDesktopShellFilePath("DesktopTrayController.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(trayController, Does.Contain("_openFolderMenuItem"));
            Assert.That(trayController, Does.Contain("_syncNowMenuItem"));
            Assert.That(trayController, Does.Contain("_settingsMenuItem"));
            Assert.That(trayController, Does.Contain("SetMenuItemEnabled(_openFolderMenuItem"));
            Assert.That(trayController, Does.Contain("nameof(ShellViewModel.SelectedSyncPair)"));
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
