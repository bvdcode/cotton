// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Localization;
using Cotton.Database.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Models.Configuration;
using Cotton.Storage.Abstractions;
using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace Cotton.Server.Services
{
    internal class CapacityCacheEntry(StorageCapacitySnapshot? snapshot)
    {
        /// <summary>
        /// Gets the snapshot.
        /// </summary>
        public StorageCapacitySnapshot? Snapshot { get; } = snapshot;

        /// <summary>
        /// Gets or sets the reserved bytes.
        /// </summary>
        public long ReservedBytes { get; private set; }

        /// <summary>
        /// Reserves bytes against the cached storage-capacity snapshot.
        /// </summary>
        public void Reserve(long bytes)
        {
            long safeBytes = Math.Max(0, bytes);
            ReservedBytes = safeBytes > long.MaxValue - ReservedBytes
                ? long.MaxValue
                : ReservedBytes + safeBytes;
        }

        /// <summary>
        /// Releases previously reserved bytes after a failed or abandoned write.
        /// </summary>
        public void Release(long bytes)
        {
            ReservedBytes = Math.Max(0, ReservedBytes - Math.Max(0, bytes));
        }
    }
}
