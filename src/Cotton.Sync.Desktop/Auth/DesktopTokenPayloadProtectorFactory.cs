// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Auth;

internal static class DesktopTokenPayloadProtectorFactory
{
    public static ITokenPayloadProtector CreateDefault()
    {
        return OperatingSystem.IsWindows()
            ? new WindowsDpapiTokenPayloadProtector()
            : new RestrictedFileTokenPayloadProtector();
    }
}
