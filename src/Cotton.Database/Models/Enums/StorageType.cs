// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Database.Models.Enums
{
    public enum StorageType
    {
        Local = 0,
        /// <summary>
        /// Use S3-compatible object storage.
        /// </summary>
        S3 = 1,
    }
}
