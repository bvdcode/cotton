// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Shell;

internal static class DesktopTrayStatusResolver
{
    private const string ToolTipPrefix = "Cotton Sync";

    public static DesktopTrayStatus FromShellState(
        bool isSignedIn,
        string globalStatus,
        bool hasActionRequired)
    {
        if (!isSignedIn)
        {
            return Create(DesktopTrayStatusKind.SignedOut, "Signed out");
        }

        if (hasActionRequired || Contains(globalStatus, "action") || Contains(globalStatus, "failed"))
        {
            return Create(DesktopTrayStatusKind.Error, "Action required");
        }

        if (Contains(globalStatus, "offline"))
        {
            return Create(DesktopTrayStatusKind.Offline, "Offline");
        }

        if (Contains(globalStatus, "paused"))
        {
            return Create(DesktopTrayStatusKind.Paused, "Paused");
        }

        if (Contains(globalStatus, "sync"))
        {
            return Create(DesktopTrayStatusKind.Syncing, "Syncing");
        }

        return Create(DesktopTrayStatusKind.Idle, "Connected");
    }

    private static DesktopTrayStatus Create(DesktopTrayStatusKind kind, string label)
    {
        return new DesktopTrayStatus(kind, ToolTipPrefix + " - " + label);
    }

    private static bool Contains(string value, string expected)
    {
        return value.Contains(expected, StringComparison.OrdinalIgnoreCase);
    }
}
