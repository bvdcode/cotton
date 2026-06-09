// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.State;

namespace Cotton.Sync
{

    internal static class SyncPathLookup
    {
        public static void Add<T>(
            Dictionary<string, T> entriesByPath,
            T entry,
            Func<T, string> pathSelector)
        {
            string relativePath = pathSelector(entry);
            string key = SyncPath.ToKey(relativePath);
            if (entriesByPath.TryGetValue(key, out T? existing))
            {
                throw new SyncPathCollisionException(pathSelector(existing), relativePath);
            }

            entriesByPath[key] = entry;
        }
    }
}
