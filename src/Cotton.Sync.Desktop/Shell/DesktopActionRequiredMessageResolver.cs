// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Shell;

internal static class DesktopActionRequiredMessageResolver
{
    public static string FromStatus(DesktopSyncStatusSnapshot status)
    {
        ArgumentNullException.ThrowIfNull(status);
        DesktopSyncPairStatusSnapshot? failedPair = status.SyncPairs
            .FirstOrDefault(static pair => !string.IsNullOrWhiteSpace(pair.LastError));
        return failedPair?.LastError ?? string.Empty;
    }

    public static string FromSelfTest(DesktopSelfTestSnapshot selfTest)
    {
        ArgumentNullException.ThrowIfNull(selfTest);
        if (selfTest.Passed)
        {
            return string.Empty;
        }

        return selfTest.Items.FirstOrDefault(static item => !item.Passed)?.Details ?? "Self-test failed.";
    }
}
