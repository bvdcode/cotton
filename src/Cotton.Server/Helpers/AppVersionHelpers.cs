// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Helpers
{
    public static class AppVersionHelpers
    {
        public const string AppVersionEnvironmentVariable = "APP_VERSION";

        public static string? GetAppVersion()
        {
            return Environment.GetEnvironmentVariable(AppVersionEnvironmentVariable);
        }
    }
}
