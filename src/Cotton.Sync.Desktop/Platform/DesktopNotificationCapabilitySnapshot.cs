// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Platform
{
    internal record DesktopNotificationCapabilitySnapshot(
        DesktopNotificationPlatform Platform,
        string AdapterName,
        bool IsSupported,
        string AppName,
        string? AppUserModelId,
        string? ExecutablePath,
        string? IconPath,
        string? PlatformDetails = null)
    {
        public string Details
        {
            get
            {
                List<string> parts =
                [
                    IsSupported ? "Supported" : "Not available on this platform",
                    "adapter: " + AdapterName,
                    "app name: " + AppName
                ];
                if (!string.IsNullOrWhiteSpace(AppUserModelId))
                {
                    parts.Add("AppUserModelID: " + AppUserModelId);
                }

                if (!string.IsNullOrWhiteSpace(ExecutablePath))
                {
                    parts.Add("executable: " + ExecutablePath);
                }

                parts.Add(IconPath is null ? "icon: missing" : "icon: " + IconPath);
                if (!string.IsNullOrWhiteSpace(PlatformDetails))
                {
                    parts.Add(PlatformDetails);
                }

                return string.Join("; ", parts);
            }
        }
    }
}
