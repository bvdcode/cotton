// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System;
using System.IO;

namespace Cotton.Crypto.Internals
{
    internal static class FormatConstants
    {
        // CTN1 was emitted before authenticated stream terminators existed.
        // New writes use CTN2, which lets readers require the terminator without breaking existing blobs.
        [Obsolete("OBSOLETE TRANSITION: CTN1 read support exists only until the CTN2 rewrite job has completed. Remove this legacy version after that transition.")]
        public const int LegacyVersion = 1;
        public const int CurrentVersion = 2;

        [Obsolete("OBSOLETE TRANSITION: CTN1 read support exists only until the CTN2 rewrite job has completed. Remove this legacy magic after that transition.")]
        public static ReadOnlySpan<byte> LegacyMagicBytes => "CTN1"u8;
        public static ReadOnlySpan<byte> CurrentMagicBytes => "CTN2"u8;
        public static ReadOnlySpan<byte> MagicBytes => CurrentMagicBytes;

        public static ReadOnlySpan<byte> GetMagicBytes(int formatVersion)
        {
            return formatVersion switch
            {
                LegacyVersion => LegacyMagicBytes,
                CurrentVersion => CurrentMagicBytes,
                _ => throw new InvalidDataException($"Unsupported Cotton crypto format version {formatVersion}.")
            };
        }

        public static bool TryGetVersion(ReadOnlySpan<byte> magic, out int formatVersion)
        {
            if (magic.SequenceEqual(CurrentMagicBytes))
            {
                formatVersion = CurrentVersion;
                return true;
            }

            if (magic.SequenceEqual(LegacyMagicBytes))
            {
                formatVersion = LegacyVersion;
                return true;
            }

            formatVersion = 0;
            return false;
        }

        public static bool RequiresAuthenticatedTerminator(int formatVersion)
            => formatVersion >= CurrentVersion;
    }
}
