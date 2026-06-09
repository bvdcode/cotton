// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Desktop.Shell;

namespace Cotton.Sync.Desktop.Tests.Shell
{

    public sealed class DesktopTrayStatusResolverTests
    {
        [Test]
        public void FromShellState_ReturnsSignedOutWhenSessionIsMissing()
        {
            DesktopTrayStatus status = DesktopTrayStatusResolver.FromShellState(
                isSignedIn: false,
                statusText: "Connected",
                hasStatusAttention: false);

            Assert.Multiple(() =>
            {
                Assert.That(status.Kind, Is.EqualTo(DesktopTrayStatusKind.SignedOut));
                Assert.That(status.ToolTipText, Is.EqualTo("Cotton Sync - Signed out"));
                Assert.That(status.IconUri.ToString(), Does.EndWith("/Assets/tray-signed-out.png"));
            });
        }

        [Test]
        public void FromShellState_ReturnsErrorWhenActionIsRequired()
        {
            DesktopTrayStatus status = DesktopTrayStatusResolver.FromShellState(
                isSignedIn: true,
                statusText: "Connected",
                hasStatusAttention: true);

            Assert.Multiple(() =>
            {
                Assert.That(status.Kind, Is.EqualTo(DesktopTrayStatusKind.Error));
                Assert.That(status.ToolTipText, Is.EqualTo("Cotton Sync - Action required"));
                Assert.That(status.IconUri.ToString(), Does.EndWith("/Assets/tray-error.png"));
            });
        }

        [Test]
        public void FromShellState_ReturnsErrorWhenConflictsNeedReview()
        {
            DesktopTrayStatus status = DesktopTrayStatusResolver.FromShellState(
                isSignedIn: true,
                statusText: "Conflicts need review",
                hasStatusAttention: true);

            Assert.Multiple(() =>
            {
                Assert.That(status.Kind, Is.EqualTo(DesktopTrayStatusKind.Error));
                Assert.That(status.ToolTipText, Is.EqualTo("Cotton Sync - Conflicts need review"));
                Assert.That(status.IconUri.ToString(), Does.EndWith("/Assets/tray-error.png"));
            });
        }

        [Test]
        public void FromShellState_ReturnsOfflineWhenGlobalStatusIsOffline()
        {
            DesktopTrayStatus status = DesktopTrayStatusResolver.FromShellState(
                isSignedIn: true,
                statusText: "Offline",
                hasStatusAttention: false);

            Assert.That(status.Kind, Is.EqualTo(DesktopTrayStatusKind.Offline));
        }

        [Test]
        public void FromShellState_ReturnsPausedWhenGlobalStatusIsPaused()
        {
            DesktopTrayStatus status = DesktopTrayStatusResolver.FromShellState(
                isSignedIn: true,
                statusText: "Paused",
                hasStatusAttention: false);

            Assert.That(status.Kind, Is.EqualTo(DesktopTrayStatusKind.Paused));
        }

        [Test]
        public void FromShellState_ReturnsIdleWhenSyncTextHasNoActiveProgress()
        {
            DesktopTrayStatus status = DesktopTrayStatusResolver.FromShellState(
                isSignedIn: true,
                statusText: "Sync requested",
                hasStatusAttention: false);

            Assert.Multiple(() =>
            {
                Assert.That(status.Kind, Is.EqualTo(DesktopTrayStatusKind.Idle));
                Assert.That(status.IconUri.ToString(), Does.EndWith("/Assets/icon-192.png"));
            });
        }

        [Test]
        public void FromShellState_ReturnsSyncingWhenWorkProgressIsActive()
        {
            DesktopTrayStatus status = DesktopTrayStatusResolver.FromShellState(
                isSignedIn: true,
                statusText: "Connected",
                hasStatusAttention: false,
                hasActiveSyncProgress: true);

            Assert.Multiple(() =>
            {
                Assert.That(status.Kind, Is.EqualTo(DesktopTrayStatusKind.Syncing));
                Assert.That(status.ToolTipText, Is.EqualTo("Cotton Sync - Syncing"));
                Assert.That(status.IconUri.ToString(), Does.EndWith("/Assets/tray-syncing.png"));
            });
        }

        [Test]
        public void FromShellState_AddsActiveProgressToSyncingTooltip()
        {
            DesktopTrayStatus status = DesktopTrayStatusResolver.FromShellState(
                isSignedIn: true,
                statusText: "Connected",
                hasStatusAttention: false,
                hasActiveSyncProgress: true,
                activeProgressTitle: "Syncing 2 folders",
                activeProgressDetails: "10 of 40 files across 2 folders",
                activeProgressHeaderDetails: "6.0 MB / 24 MB · 3.0 MB/s · 6s left");

            Assert.Multiple(() =>
            {
                Assert.That(status.Kind, Is.EqualTo(DesktopTrayStatusKind.Syncing));
                Assert.That(status.ToolTipText, Is.EqualTo("Cotton Sync - Syncing 2 folders - 6.0 MB / 24 MB · 3.0 MB/s · 6s left"));
            });
        }

        [Test]
        public void FromShellState_UsesRunProgressDetailsInSyncingTooltipWhenHeaderDetailsAreMissing()
        {
            DesktopTrayStatus status = DesktopTrayStatusResolver.FromShellState(
                isSignedIn: true,
                statusText: "Connected",
                hasStatusAttention: false,
                hasActiveSyncProgress: true,
                activeProgressTitle: "Documents: Scanning local files",
                activeProgressDetails: "123 files found · report.txt");

            Assert.That(status.ToolTipText, Is.EqualTo("Cotton Sync - Documents: Scanning local files - 123 files found · report.txt"));
        }

        [Test]
        public void FromShellState_ReturnsIdleWhenSignedInAndNoStatusMatches()
        {
            DesktopTrayStatus status = DesktopTrayStatusResolver.FromShellState(
                isSignedIn: true,
                statusText: "Connected",
                hasStatusAttention: false);

            Assert.Multiple(() =>
            {
                Assert.That(status.Kind, Is.EqualTo(DesktopTrayStatusKind.Idle));
                Assert.That(status.ToolTipText, Is.EqualTo("Cotton Sync - Connected"));
            });
        }

        [Test]
        public void Resolve_ReturnsPlainCottonIconForIdleState()
        {
            Uri iconUri = DesktopTrayIconAssetResolver.Resolve(DesktopTrayStatusKind.Idle);

            Assert.That(iconUri.ToString(), Does.EndWith("/Assets/icon-192.png"));
        }

        [Test]
        public void Resolve_ReturnsDedicatedIconForNonIdleTrayStates()
        {
            (DesktopTrayStatusKind Kind, string AssetName)[] cases =
            [
                (DesktopTrayStatusKind.SignedOut, "tray-signed-out.png"),
                (DesktopTrayStatusKind.Syncing, "tray-syncing.png"),
                (DesktopTrayStatusKind.Paused, "tray-paused.png"),
                (DesktopTrayStatusKind.Offline, "tray-offline.png"),
                (DesktopTrayStatusKind.Error, "tray-error.png"),
            ];

            foreach ((DesktopTrayStatusKind kind, string assetName) in cases)
            {
                Uri iconUri = DesktopTrayIconAssetResolver.Resolve(kind);

                Assert.That(iconUri.ToString(), Does.EndWith("/Assets/" + assetName));
            }
        }

        [Test]
        public void Resolve_RejectsUnknownState()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => DesktopTrayIconAssetResolver.Resolve(DesktopTrayStatusKind.Unknown));
        }
    }
}
