// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Storage.Abstractions
{
    /// <summary>
    /// Reports physical capacity for backends that can cheaply expose free-space information.
    /// </summary>
    public interface IStorageCapacityReporter
    {
        /// <summary>Returns a point-in-time capacity snapshot.</summary>
        StorageCapacitySnapshot GetCapacitySnapshot();
    }
}
