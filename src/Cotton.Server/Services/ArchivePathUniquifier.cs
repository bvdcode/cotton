// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    internal class ArchivePathUniquifier
    {
        private readonly HashSet<string> _occupied = new(StringComparer.OrdinalIgnoreCase);

        public string AddDirectory(string path)
        {
            string normalized = Normalize(path).TrimEnd('/');
            string candidate = normalized;
            for (int suffix = 2; !_occupied.Add(candidate); suffix++)
            {
                candidate = AppendSuffix(normalized, suffix);
            }

            return candidate + "/";
        }

        public string AddFile(string path)
        {
            string normalized = Normalize(path).TrimEnd('/');
            string candidate = normalized;
            for (int suffix = 2; !_occupied.Add(candidate); suffix++)
            {
                candidate = AppendSuffix(normalized, suffix);
            }

            return candidate;
        }

        public static string Combine(string parentPath, string name)
        {
            string normalizedParent = Normalize(parentPath).TrimEnd('/');
            string normalizedName = Normalize(name).Trim('/');
            return string.IsNullOrEmpty(normalizedParent)
                ? normalizedName
                : normalizedParent + "/" + normalizedName;
        }

        private static string Normalize(string path)
        {
            string normalized = path.Replace('\\', '/').Trim('/');
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new InvalidOperationException("Archive entry path cannot be empty.");
            }

            return normalized;
        }

        private static string AppendSuffix(string path, int suffix)
        {
            int slashIndex = path.LastIndexOf('/');
            string directory = slashIndex >= 0 ? path[..(slashIndex + 1)] : string.Empty;
            string name = slashIndex >= 0 ? path[(slashIndex + 1)..] : path;
            int dotIndex = name.LastIndexOf('.');
            if (dotIndex <= 0)
            {
                return $"{directory}{name} ({suffix})";
            }

            return $"{directory}{name[..dotIndex]} ({suffix}){name[dotIndex..]}";
        }
    }
}
