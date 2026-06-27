// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto.Internals;

namespace Cotton.Crypto
{
    /// <summary>
    /// Exposes Cotton encrypted stream format markers for migration and diagnostics code.
    /// </summary>
    public static class CottonEncryptedStreamFormat
    {
        /// <summary>
        /// Number of bytes occupied by the stream magic prefix.
        /// </summary>
        public const int MagicByteLength = 4;

        /// <summary>
        /// Current encrypted stream format version emitted by all new writes.
        /// </summary>
        public const int CurrentVersion = FormatConstants.CurrentVersion;

        /// <summary>
        /// Legacy encrypted stream format version accepted only during the CTN2 transition.
        /// </summary>
        [Obsolete("OBSOLETE TRANSITION: CTN1 read support exists only until the CTN2 rewrite job has completed. Remove this legacy version after that transition.")]
        public const int LegacyVersion = FormatConstants.LegacyVersion;

        /// <summary>
        /// Current encrypted stream magic prefix.
        /// </summary>
        public static ReadOnlySpan<byte> CurrentMagicBytes => FormatConstants.CurrentMagicBytes;

        /// <summary>
        /// Legacy encrypted stream magic prefix accepted only during the CTN2 transition.
        /// </summary>
        [Obsolete("OBSOLETE TRANSITION: CTN1 read support exists only until the CTN2 rewrite job has completed. Remove this legacy magic after that transition.")]
        public static ReadOnlySpan<byte> LegacyMagicBytes => FormatConstants.LegacyMagicBytes;

        /// <summary>
        /// Attempts to resolve an encrypted stream format version from a magic prefix.
        /// </summary>
        public static bool TryGetVersion(ReadOnlySpan<byte> magic, out int formatVersion)
        {
            return FormatConstants.TryGetVersion(magic, out formatVersion);
        }
    }
}
