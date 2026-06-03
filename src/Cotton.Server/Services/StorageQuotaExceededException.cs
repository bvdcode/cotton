// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services;

/// <summary>
/// Represents a write rejected because the user's logical storage quota would be exceeded.
/// </summary>
public sealed class StorageQuotaExceededException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StorageQuotaExceededException" /> class.
    /// </summary>
    public StorageQuotaExceededException(long usedBytes, long quotaBytes, long additionalBytes)
        : base(
            "Storage quota exceeded. Current usage is "
            + usedBytes
            + " bytes, quota is "
            + quotaBytes
            + " bytes, requested additional bytes is "
            + additionalBytes
            + ".")
    {
        UsedBytes = usedBytes;
        QuotaBytes = quotaBytes;
        AdditionalBytes = additionalBytes;
    }

    /// <summary>
    /// Gets the user's current logical usage in bytes.
    /// </summary>
    public long UsedBytes { get; }

    /// <summary>
    /// Gets the user's configured quota in bytes.
    /// </summary>
    public long QuotaBytes { get; }

    /// <summary>
    /// Gets the additional logical bytes requested by the failed operation.
    /// </summary>
    public long AdditionalBytes { get; }
}
