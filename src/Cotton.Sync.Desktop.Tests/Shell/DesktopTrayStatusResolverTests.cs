// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Desktop.Shell;

namespace Cotton.Sync.Desktop.Tests.Shell;

public sealed class DesktopTrayStatusResolverTests
{
    [Test]
    public void FromShellState_ReturnsSignedOutWhenSessionIsMissing()
    {
        DesktopTrayStatus status = DesktopTrayStatusResolver.FromShellState(
            isSignedIn: false,
            globalStatus: "Connected",
            hasActionRequired: false);

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
            globalStatus: "Connected",
            hasActionRequired: true);

        Assert.Multiple(() =>
        {
            Assert.That(status.Kind, Is.EqualTo(DesktopTrayStatusKind.Error));
            Assert.That(status.ToolTipText, Is.EqualTo("Cotton Sync - Action required"));
            Assert.That(status.IconUri.ToString(), Does.EndWith("/Assets/tray-error.png"));
        });
    }

    [Test]
    public void FromShellState_ReturnsOfflineWhenGlobalStatusIsOffline()
    {
        DesktopTrayStatus status = DesktopTrayStatusResolver.FromShellState(
            isSignedIn: true,
            globalStatus: "Offline",
            hasActionRequired: false);

        Assert.That(status.Kind, Is.EqualTo(DesktopTrayStatusKind.Offline));
    }

    [Test]
    public void FromShellState_ReturnsPausedWhenGlobalStatusIsPaused()
    {
        DesktopTrayStatus status = DesktopTrayStatusResolver.FromShellState(
            isSignedIn: true,
            globalStatus: "Paused",
            hasActionRequired: false);

        Assert.That(status.Kind, Is.EqualTo(DesktopTrayStatusKind.Paused));
    }

    [Test]
    public void FromShellState_ReturnsSyncingWhenGlobalStatusIsSyncing()
    {
        DesktopTrayStatus status = DesktopTrayStatusResolver.FromShellState(
            isSignedIn: true,
            globalStatus: "Sync requested",
            hasActionRequired: false);

        Assert.That(status.Kind, Is.EqualTo(DesktopTrayStatusKind.Syncing));
    }

    [Test]
    public void FromShellState_ReturnsIdleWhenSignedInAndNoStatusMatches()
    {
        DesktopTrayStatus status = DesktopTrayStatusResolver.FromShellState(
            isSignedIn: true,
            globalStatus: "Connected",
            hasActionRequired: false);

        Assert.Multiple(() =>
        {
            Assert.That(status.Kind, Is.EqualTo(DesktopTrayStatusKind.Idle));
            Assert.That(status.ToolTipText, Is.EqualTo("Cotton Sync - Connected"));
        });
    }

    [Test]
    public void Resolve_ReturnsDedicatedIconForTrayStates()
    {
        (DesktopTrayStatusKind Kind, string AssetName)[] cases =
        [
            (DesktopTrayStatusKind.Idle, "tray-idle.png"),
            (DesktopTrayStatusKind.Syncing, "tray-syncing.png"),
            (DesktopTrayStatusKind.Paused, "tray-paused.png"),
            (DesktopTrayStatusKind.Offline, "tray-offline.png"),
            (DesktopTrayStatusKind.Error, "tray-error.png"),
            (DesktopTrayStatusKind.SignedOut, "tray-signed-out.png"),
        ];

        foreach ((DesktopTrayStatusKind kind, string assetName) in cases)
        {
            Uri iconUri = DesktopTrayIconAssetResolver.Resolve(kind);

            Assert.That(iconUri.ToString(), Does.EndWith("/Assets/" + assetName));
        }
    }
}
