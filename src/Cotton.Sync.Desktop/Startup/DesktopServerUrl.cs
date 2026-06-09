// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sdk;

namespace Cotton.Sync.Desktop.Startup
{

    internal static class DesktopServerUrl
    {
        public static Uri? NormalizeOptional(string? value)
        {
            return CottonServerUrl.NormalizeOptional(value);
        }

        public static Uri NormalizeRequired(string value, string parameterName)
        {
            return CottonServerUrl.NormalizeRequired(value, parameterName);
        }
    }
}
