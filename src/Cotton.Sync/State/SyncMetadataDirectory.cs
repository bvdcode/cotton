// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.State
{
    /// <summary>
    /// Provides the metadata directory used inside local sync roots.
    /// </summary>
    public static class SyncMetadataDirectory
    {
        /// <summary>
        /// Directory name reserved for local synchronization metadata inside a sync root.
        /// </summary>
        public const string Name = ".cotton-sync";

        /// <summary>
        /// Ensures the local metadata directory exists and applies platform-specific directory attributes.
        /// </summary>
        public static string Ensure(string rootPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
            string metadataDirectoryPath = GetPath(rootPath);
            DirectoryInfo metadataDirectory = Directory.CreateDirectory(metadataDirectoryPath);
            HideOnWindows(metadataDirectory);
            return metadataDirectory.FullName;
        }

        /// <summary>
        /// Applies platform-specific directory attributes to an existing local metadata directory.
        /// </summary>
        public static void HideIfExists(string rootPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
            var metadataDirectory = new DirectoryInfo(GetPath(rootPath));
            if (metadataDirectory.Exists)
            {
                HideOnWindows(metadataDirectory);
            }
        }

        /// <summary>
        /// Gets the full local metadata directory path for a sync root.
        /// </summary>
        public static string GetPath(string rootPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
            return Path.Combine(Path.GetFullPath(rootPath), Name);
        }

        private static void HideOnWindows(DirectoryInfo metadataDirectory)
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            metadataDirectory.Refresh();
            if ((metadataDirectory.Attributes & FileAttributes.Hidden) == 0)
            {
                metadataDirectory.Attributes |= FileAttributes.Hidden;
            }
        }
    }
}
