// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.State;

namespace Cotton.Sync
{
    internal class DirectoryContentIndex
    {
        private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;
        private readonly HashSet<string> _directoryKeysWithChildren;

        private DirectoryContentIndex(HashSet<string> directoryKeysWithChildren)
        {
            _directoryKeysWithChildren = directoryKeysWithChildren;
        }

        public static DirectoryContentIndex Empty { get; } = new([]);

        public static DirectoryContentIndex Create(IEnumerable<string> directoryKeys, IEnumerable<string> fileKeys)
        {
            var directoryKeysWithChildren = new HashSet<string>(PathComparer);
            AddAncestorDirectoryKeys(directoryKeysWithChildren, directoryKeys);
            AddAncestorDirectoryKeys(directoryKeysWithChildren, fileKeys);
            return new DirectoryContentIndex(directoryKeysWithChildren);
        }

        public bool HasChildren(string relativePath)
        {
            return _directoryKeysWithChildren.Contains(SyncPath.ToKey(relativePath));
        }

        private static void AddAncestorDirectoryKeys(HashSet<string> directoryKeysWithChildren, IEnumerable<string> childKeys)
        {
            foreach (string childKey in childKeys)
            {
                AddAncestorDirectoryKeys(directoryKeysWithChildren, childKey);
            }
        }

        private static void AddAncestorDirectoryKeys(HashSet<string> directoryKeysWithChildren, string childKey)
        {
            string currentKey = childKey;
            while (!string.IsNullOrEmpty(currentKey))
            {
                int separatorIndex = currentKey.LastIndexOf('/');
                if (separatorIndex < 0)
                {
                    break;
                }

                string parentKey = currentKey[..separatorIndex];
                directoryKeysWithChildren.Add(parentKey);
                currentKey = parentKey;
            }
        }
    }
}
