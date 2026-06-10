// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Reflection;

namespace Cotton.Sync.Cli
{
    internal static class SyncCliAppVersion
    {
        public static string Current => Resolve(typeof(SyncCliAppVersion).Assembly);

        internal static string Resolve(Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(assembly);
            string? informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            string resolved = string.IsNullOrWhiteSpace(informationalVersion)
                ? assembly.GetName().Version?.ToString() ?? "unknown"
                : informationalVersion;
            int metadataStart = resolved.IndexOf('+', StringComparison.Ordinal);
            return metadataStart > 0 ? resolved[..metadataStart] : resolved;
        }
    }
}
