// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Storage.Abstractions
{
    /// <summary>
    /// Free-space snapshot returned by a storage backend.
    /// </summary>
    public record StorageCapacitySnapshot(
        string Backend,
        string RootPath,
        long TotalBytes,
        long AvailableBytes)
    {
        /// <summary>Gets available storage as a percentage of total capacity.</summary>
        public double AvailablePercent => TotalBytes <= 0
            ? 100d
            : AvailableBytes * 100d / TotalBytes;
    }
}
