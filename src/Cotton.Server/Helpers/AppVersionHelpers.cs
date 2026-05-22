// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Helpers
{
    /// <summary>
    /// Contains helper methods for app version.
    /// </summary>
    public static class AppVersionHelpers
    {
        /// <summary>
        /// Defines the app version environment variable.
        /// </summary>
        public const string AppVersionEnvironmentVariable = "APP_VERSION";

        /// <summary>
        /// Gets app version.
        /// </summary>
        public static string? GetAppVersion()
        {
            return Environment.GetEnvironmentVariable(AppVersionEnvironmentVariable);
        }
    }
}
