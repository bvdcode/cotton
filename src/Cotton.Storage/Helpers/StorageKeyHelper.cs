// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Storage.Helpers
{
    /// <summary>
    /// Converts content UIDs to the sharded storage-key layout used by all backends.
    /// </summary>
    public static class StorageKeyHelper
    {
        private const int MinFileUidLength = 6;

        /// <summary>Normalizes and validates a lowercase hexadecimal storage UID.</summary>
        public static string NormalizeUid(string uid)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(uid);
            string normalized = uid.Trim().ToLowerInvariant();
            if (normalized.Length < MinFileUidLength)
            {
                throw new ArgumentException("File UID is too short, minimum length is " + MinFileUidLength);
            }
            for (int i = 0; i < normalized.Length; i++)
            {
                char c = normalized[i];
                bool isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
                if (!isHex)
                {
                    throw new ArgumentException("File UID contains invalid character: " + c);
                }
            }
            return normalized;
        }

        /// <summary>Splits a storage UID into two shard directory names and the remaining file name.</summary>
        public static (string part1, string part2, string fileName) GetSegments(string uid)
        {
            uid = NormalizeUid(uid);
            string p1 = uid[..2];
            string p2 = uid[2..4];
            string fileName = uid[4..];
            return (p1, p2, fileName);
        }
    }
}
