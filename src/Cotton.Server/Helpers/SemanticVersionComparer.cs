// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using NuGet.Versioning;

namespace Cotton.Server.Helpers
{
    /// <summary>
    /// Compares semantic version values.
    /// </summary>
    public static class SemanticVersionComparer
    {
        /// <summary>
        /// Indicates whether downgrade.
        /// </summary>
        public static bool IsDowngrade(string currentVersion, string latestVersion)
            => IsGreaterThan(latestVersion, currentVersion);

        /// <summary>
        /// Indicates whether newer.
        /// </summary>
        public static bool IsNewer(string candidateVersion, string currentVersion)
            => IsGreaterThan(candidateVersion, currentVersion);

        private static bool IsGreaterThan(string candidateVersion, string currentVersion)
        {
            return TryParse(candidateVersion, out NuGetVersion? candidate)
                && TryParse(currentVersion, out NuGetVersion? current)
                && VersionComparer.VersionRelease.Compare(candidate, current) > 0;
        }

        private static bool TryParse(string value, out NuGetVersion? version)
        {
            string normalized = value.Trim();
            if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[1..];
            }

            return NuGetVersion.TryParse(normalized, out version);
        }
    }
}
