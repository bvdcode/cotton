// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Shell
{

    internal static class DesktopTrayIconAssetResolver
    {
        public static Uri Resolve(DesktopTrayStatusKind kind)
        {
            return kind switch
            {
                DesktopTrayStatusKind.Idle => Create("icon-192.png"),
                DesktopTrayStatusKind.Syncing => Create("tray-syncing.png"),
                DesktopTrayStatusKind.Paused => Create("tray-paused.png"),
                DesktopTrayStatusKind.Offline => Create("tray-offline.png"),
                DesktopTrayStatusKind.Error => Create("tray-error.png"),
                DesktopTrayStatusKind.SignedOut => Create("tray-signed-out.png"),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown tray status cannot be resolved."),
            };
        }

        private static Uri Create(string assetName)
        {
            return new Uri("avares://Cotton.Sync.Desktop/Assets/" + assetName);
        }
    }
}
