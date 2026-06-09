// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.ViewModels
{
    internal record DesktopFeatureFlags(bool ShowFutureSyncModes)
    {
        private const string ShowFutureSyncModesVariable = "COTTON_SYNC_DESKTOP_SHOW_FUTURE_MODES";

        public static DesktopFeatureFlags Default { get; } = FromEnvironment(Environment.GetEnvironmentVariable);

        public static DesktopFeatureFlags FromEnvironment(Func<string, string?> readEnvironmentVariable)
        {
            ArgumentNullException.ThrowIfNull(readEnvironmentVariable);
            string? value = readEnvironmentVariable(ShowFutureSyncModesVariable);
            return new DesktopFeatureFlags(IsEnabled(value));
        }

        private static bool IsEnabled(string? value)
        {
            return string.Equals(value, "1", StringComparison.Ordinal)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
        }
    }
}
