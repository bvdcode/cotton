// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Cotton.Storage.Abstractions;

public interface IStorageCapacityReporter
{
    StorageCapacitySnapshot GetCapacitySnapshot();
}

public sealed record StorageCapacitySnapshot(
    string Backend,
    string RootPath,
    long TotalBytes,
    long AvailableBytes)
{
    public double AvailablePercent => TotalBytes <= 0
        ? 100d
        : AvailableBytes * 100d / TotalBytes;
}
