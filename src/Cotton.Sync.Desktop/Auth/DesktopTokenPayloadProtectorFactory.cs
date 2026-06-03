// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Desktop.Platform;

namespace Cotton.Sync.Desktop.Auth;

internal static class DesktopTokenPayloadProtectorFactory
{
    private const string SecretToolCommandName = "secret-tool";

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

        return new RestrictedFileTokenPayloadProtector();
    }

    internal static ITokenPayloadProtector CreateLinuxDefault(string? pathValue)
    {
        string? secretToolPath = ResolveExecutablePath(SecretToolCommandName, pathValue);
        return secretToolPath is null
            ? new RestrictedFileTokenPayloadProtector()
            : new LinuxSecretServiceTokenPayloadProtector(secretToolPath);
    }

    internal static string? ResolveExecutablePath(string commandName, string? pathValue)
    {
        return ExecutablePathResolver.Resolve(commandName, pathValue);
    }
}
