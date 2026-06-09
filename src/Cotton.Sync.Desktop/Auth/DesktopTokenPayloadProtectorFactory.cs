// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Desktop.Platform;

namespace Cotton.Sync.Desktop.Auth
{
    internal static class DesktopTokenPayloadProtectorFactory
    {
        private const string SecretToolCommandName = "secret-tool";
        private const string UnsupportedTokenStorageScheme = "unsupported-token-storage-v1";
        private const string UnavailableLinuxSecretServiceScheme = "linux-secret-service-unavailable-v1";

        public static ITokenPayloadProtector CreateDefault()
        {
            if (OperatingSystem.IsWindows())
            {
                return new WindowsDpapiTokenPayloadProtector();
            }

            if (OperatingSystem.IsLinux())
            {
                return CreateLinuxDefault(Environment.GetEnvironmentVariable("PATH"));
            }

            return new UnsupportedTokenPayloadProtector(
                UnsupportedTokenStorageScheme,
                "Secure token storage is not implemented for this operating system.");
        }

        internal static ITokenPayloadProtector CreateLinuxDefault(string? pathValue)
        {
            string? secretToolPath = ResolveExecutablePath(SecretToolCommandName, pathValue);
            return secretToolPath is null
                ? new UnsupportedTokenPayloadProtector(
                    UnavailableLinuxSecretServiceScheme,
                    "Linux Secret Service is unavailable because secret-tool was not found in PATH.")
                : new LinuxSecretServiceTokenPayloadProtector(secretToolPath);
        }

        internal static string? ResolveExecutablePath(string commandName, string? pathValue)
        {
            return ExecutablePathResolver.Resolve(commandName, pathValue);
        }
    }
}
