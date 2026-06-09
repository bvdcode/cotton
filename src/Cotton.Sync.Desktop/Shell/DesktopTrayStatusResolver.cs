// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Shell
{

    internal static class DesktopTrayStatusResolver
    {
        private const string ToolTipPrefix = "Cotton Sync";

        public static DesktopTrayStatus FromShellState(
            bool isSignedIn,
            string statusText,
            bool hasStatusAttention,
            bool hasActiveSyncProgress = false,
            string? activeProgressTitle = null,
            string? activeProgressDetails = null,
            string? activeProgressHeaderDetails = null)
        {
            if (!isSignedIn)
            {
                return Create(DesktopTrayStatusKind.SignedOut, "Signed out");
            }

            if (hasStatusAttention || Contains(statusText, "action") || Contains(statusText, "failed"))
            {
                return Create(
                    DesktopTrayStatusKind.Error,
                    Contains(statusText, "conflict") ? "Conflicts need review" : "Action required");
            }

            if (Contains(statusText, "offline"))
            {
                return Create(DesktopTrayStatusKind.Offline, "Offline");
            }

            if (Contains(statusText, "paused"))
            {
                return Create(DesktopTrayStatusKind.Paused, "Paused");
            }

            if (hasActiveSyncProgress)
            {
                return Create(
                    DesktopTrayStatusKind.Syncing,
                    CreateActiveSyncLabel(activeProgressTitle, activeProgressHeaderDetails, activeProgressDetails));
            }

            return Create(DesktopTrayStatusKind.Idle, "Connected");
        }

        private static string CreateActiveSyncLabel(
            string? activeProgressTitle,
            string? activeProgressHeaderDetails,
            string? activeProgressDetails)
        {
            string title = string.IsNullOrWhiteSpace(activeProgressTitle) ? "Syncing" : activeProgressTitle.Trim();
            string details = !string.IsNullOrWhiteSpace(activeProgressHeaderDetails)
                ? activeProgressHeaderDetails.Trim()
                : string.IsNullOrWhiteSpace(activeProgressDetails) ? string.Empty : activeProgressDetails.Trim();
            return string.IsNullOrWhiteSpace(details) ? title : title + " - " + details;
        }

        private static DesktopTrayStatus Create(DesktopTrayStatusKind kind, string label)
        {
            return new DesktopTrayStatus(kind, ToolTipPrefix + " - " + label, DesktopTrayIconAssetResolver.Resolve(kind));
        }

        private static bool Contains(string value, string expected)
        {
            return value.Contains(expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}
