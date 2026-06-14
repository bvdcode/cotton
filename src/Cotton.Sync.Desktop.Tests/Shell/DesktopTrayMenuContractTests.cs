// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Tests.Shell
{
    public class DesktopTrayMenuContractTests
    {
        [Test]
        public void TrayMenu_UsesDeterministicFolderAndCloudLabels()
        {
            string trayController = File.ReadAllText(GetDesktopShellFilePath("DesktopTrayController.cs"));

            Assert.Multiple(() =>
            {
                Assert.That(trayController, Does.Contain("\"Open local folder\""));
                Assert.That(trayController, Does.Contain("\"Open in web\""));
                Assert.That(trayController, Does.Contain("TrayOpenFolderLabel"));
                Assert.That(trayController, Does.Contain("OpenTrayFolderCommand"));
                Assert.That(trayController, Does.Not.Contain("\"Open selected folder\""));
                Assert.That(trayController, Does.Not.Contain("\"Open folder\""));
                Assert.That(trayController, Does.Not.Contain("\"Web app\""));
                Assert.That(trayController, Does.Not.Contain("\"Open in Cotton Cloud\""));
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
                Assert.That(trayController, Does.Contain("RebuildTrayMenu("));
                Assert.That(trayController, Does.Contain("_trayIcon.Menu.Items.Clear()"));
                Assert.That(trayController, Does.Contain("AddMenuItemIf(showOpenFolder, _openFolderMenuItem)"));
                Assert.That(trayController, Does.Contain("AddMenuItemIf(showOpenWeb, _openWebMenuItem)"));
                Assert.That(trayController, Does.Contain("AddMenuItemIf(showSyncNow, _syncNowMenuItem)"));
                Assert.That(trayController, Does.Contain("AddMenuItemIf(showPauseResume, _pauseResumeMenuItem)"));
                Assert.That(trayController, Does.Contain("AddMenuItemIf(showSettings, _settingsMenuItem)"));
                Assert.That(trayController, Does.Contain("nameof(ShellViewModel.CanOpenTrayFolder)"));
                Assert.That(trayController, Does.Contain("nameof(ShellViewModel.TrayOpenFolderLabel)"));
                Assert.That(trayController, Does.Contain("nameof(ShellViewModel.HasCurrentWorkProgress)"));
                Assert.That(trayController, Does.Not.Contain("menuItem.IsEnabled = isAvailable"));
                Assert.That(trayController, Does.Not.Contain("menuItem.IsVisible = isAvailable"));
                Assert.That(trayController, Does.Not.Contain("SetMenuItemAvailability"));
                Assert.That(trayController, Does.Not.Contain("SetMenuItemEnabled"));
                Assert.That(trayController, Does.Not.Contain("SetMenuItemEnabled(_openFolderMenuItem"));
                Assert.That(trayController, Does.Not.Contain("nameof(ShellViewModel.SelectedSyncPair)"));
            });
        }

        [Test]
        public void TrayMenu_DispatchesWindowActionsToAvaloniaUiThread()
        {
            string trayController = File.ReadAllText(GetDesktopShellFilePath("DesktopTrayController.cs"));

            Assert.Multiple(() =>
            {
                Assert.That(trayController, Does.Contain("using Avalonia.Threading;"));
                Assert.That(trayController, Does.Contain("RunOnUiThread(action)"));
                Assert.That(trayController, Does.Contain("RunOnUiThread(ShowWindow)"));
                Assert.That(trayController, Does.Contain("Dispatcher.UIThread.CheckAccess()"));
                Assert.That(trayController, Does.Contain("Dispatcher.UIThread.Post(action)"));
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
}
