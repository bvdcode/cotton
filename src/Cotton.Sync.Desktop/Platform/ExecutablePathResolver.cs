// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Platform
{

    internal static class ExecutablePathResolver
    {
        public static string? Resolve(string commandName, string? pathValue)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
            if (string.IsNullOrWhiteSpace(pathValue))
            {
                return null;
            }

            foreach (string pathPart in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = Path.Combine(pathPart, commandName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
