// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Storage.Abstractions;

/// <summary>
/// Reports physical capacity for backends that can cheaply expose free-space information.
/// </summary>
public interface IStorageCapacityReporter
{
    /// <summary>Returns a point-in-time capacity snapshot.</summary>
    StorageCapacitySnapshot GetCapacitySnapshot();
}

/// <summary>
/// Free-space snapshot returned by a storage backend.
/// </summary>
public sealed record StorageCapacitySnapshot(
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
