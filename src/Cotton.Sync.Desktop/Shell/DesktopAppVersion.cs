// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Reflection;

namespace Cotton.Sync.Desktop.Shell
{
    internal static class DesktopAppVersion
    {
        public static string Current => Resolve(typeof(DesktopAppVersion).Assembly);

        internal static string Resolve(Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(assembly);
            string? informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            return string.IsNullOrWhiteSpace(informationalVersion)
                ? assembly.GetName().Version?.ToString() ?? "unknown"
                : informationalVersion;
        }
    }
}
