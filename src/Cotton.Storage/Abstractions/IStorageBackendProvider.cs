// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Storage.Abstractions
{
    /// <summary>
    /// Resolves the active storage backend from current application settings.
    /// </summary>
    public interface IStorageBackendProvider
    {
        /// <summary>
        /// Returns the backend used for the current operation.
        /// </summary>
        IStorageBackend GetBackend();
    }
}
