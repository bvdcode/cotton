// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Auth;

internal static class DesktopTokenStorageCapabilities
{
    public static DesktopTokenStorageCapabilitySnapshot CreateSnapshot()
    {
        return CreateSnapshot(DesktopTokenPayloadProtectorFactory.CreateDefault());
    }

    internal static DesktopTokenStorageCapabilitySnapshot CreateSnapshot(ITokenPayloadProtector protector)
    {
        ArgumentNullException.ThrowIfNull(protector);
        return protector switch
        {
            WindowsDpapiTokenPayloadProtector => new DesktopTokenStorageCapabilitySnapshot(
                protector.Scheme,
                IsReleaseSecure: true,
                "Windows DPAPI current-user protection"),
            LinuxSecretServiceTokenPayloadProtector => new DesktopTokenStorageCapabilitySnapshot(
                protector.Scheme,
                IsReleaseSecure: true,
                "Linux Secret Service through secret-tool"),
            RestrictedFileTokenPayloadProtector => new DesktopTokenStorageCapabilitySnapshot(
                protector.Scheme,
                IsReleaseSecure: false,
                "Development fallback: restricted local file without cryptographic protection"),
            _ => new DesktopTokenStorageCapabilitySnapshot(
                protector.Scheme,
                IsReleaseSecure: false,
                "Unknown token payload protector"),
        };
    }
}
