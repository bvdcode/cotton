// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Desktop.Shell;
using Cotton.Sync.Desktop.Startup;

namespace Cotton.Sync.Desktop.Tests.Shell
{
    public class VisualSmokeShellControllerTests
    {
        [Test]
        public async Task LoadAsync_ReturnsSignedInDashboardSnapshot()
        {
            using VisualSmokeShellController controller = VisualSmokeShellController.Create(DesktopVisualSmokeScenario.Dashboard);

            DesktopShellSnapshot snapshot = await controller.LoadAsync();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.IsSignedIn, Is.True);
                Assert.That(snapshot.ServerUrl, Is.EqualTo(new Uri("https://app.cottoncloud.dev/")));
                Assert.That(snapshot.AccountName, Is.EqualTo("qa@cottoncloud.dev"));
                Assert.That(snapshot.SyncPairs, Has.Count.EqualTo(2));
                Assert.That(snapshot.SyncPairs[0].Status, Is.EqualTo("Idle"));
                Assert.That(snapshot.SyncPairs[0].LastSyncedAtUtc, Is.Not.Null);
            });
        }

        [Test]
        public async Task LoadAsync_ReturnsErrorPairForErrorScenario()
        {
            using VisualSmokeShellController controller = VisualSmokeShellController.Create(DesktopVisualSmokeScenario.Error);

            DesktopShellSnapshot snapshot = await controller.LoadAsync();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.SyncPairs[0].Status, Is.EqualTo("Error"));
                Assert.That(
                    snapshot.SyncPairs[0].LastError,
                    Is.EqualTo(DesktopActionRequiredMessageResolver.MissingDesktopSyncChangesApiMessage));
            });
        }

        [Test]
        public async Task LoadAsync_ReturnsSyncingPairForProgressScenario()
        {
            using VisualSmokeShellController controller = VisualSmokeShellController.Create(DesktopVisualSmokeScenario.Progress);

            DesktopShellSnapshot snapshot = await controller.LoadAsync();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.IsSignedIn, Is.True);
                Assert.That(snapshot.SyncPairs, Has.Count.EqualTo(2));
                Assert.That(snapshot.SyncPairs[0].Status, Is.EqualTo("Syncing"));
                Assert.That(snapshot.SyncPairs[0].LastError, Is.Null);
            });
        }

        [Test]
        public async Task LoadAsync_ReturnsSignedInEmptyDashboardForAddFolderScenario()
        {
            using VisualSmokeShellController controller = VisualSmokeShellController.Create(DesktopVisualSmokeScenario.AddFolder);

            DesktopShellSnapshot snapshot = await controller.LoadAsync();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.IsSignedIn, Is.True);
                Assert.That(snapshot.SyncPairs, Is.Empty);
                Assert.That(snapshot.AccountName, Is.EqualTo("qa@cottoncloud.dev"));
            });
        }

        [Test]
        public async Task LoadAsync_ReturnsSignedInEmptyDashboardForEmptyDashboardScenario()
        {
            using VisualSmokeShellController controller = VisualSmokeShellController.Create(DesktopVisualSmokeScenario.EmptyDashboard);

            DesktopShellSnapshot snapshot = await controller.LoadAsync();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.IsSignedIn, Is.True);
                Assert.That(snapshot.SyncPairs, Is.Empty);
                Assert.That(snapshot.AccountName, Is.EqualTo("qa@cottoncloud.dev"));
            });
        }

        [Test]
        public async Task LoadAsync_ReturnsSignedOutSetupSnapshotForSignInErrorScenario()
        {
            using VisualSmokeShellController controller = VisualSmokeShellController.Create(DesktopVisualSmokeScenario.SignInError);

            DesktopShellSnapshot snapshot = await controller.LoadAsync();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.IsSignedIn, Is.False);
                Assert.That(snapshot.SyncPairs, Is.Empty);
                Assert.That(snapshot.ServerUrl, Is.EqualTo(new Uri("https://app.cottoncloud.dev/")));
                Assert.That(snapshot.RememberedUsername, Is.EqualTo("qa@cottoncloud.dev"));
            });
        }
    }
}
