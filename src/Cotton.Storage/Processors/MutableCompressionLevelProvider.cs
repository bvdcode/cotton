// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Storage.Processors
{
    /// <summary>
    /// Keeps the runtime Zstandard compression level for diagnostic tuning.
    /// </summary>
    public sealed class MutableCompressionLevelProvider : ICompressionLevelProvider
    {
        private int _level = CompressionProcessor.DefaultCompressionLevel;

        /// <inheritdoc />
        public int Level => Volatile.Read(ref _level);

        /// <summary>
        /// Sets the compression level used by future writes and returns the previous value.
        /// </summary>
        public int SetLevel(int level)
        {
            CompressionProcessor.ThrowIfInvalidLevel(level);
            return Interlocked.Exchange(ref _level, level);
        }

        /// <summary>
        /// Restores the default Zstandard compression level and returns the previous value.
        /// </summary>
        public int Reset()
        {
            return Interlocked.Exchange(ref _level, CompressionProcessor.DefaultCompressionLevel);
        }
    }
}
