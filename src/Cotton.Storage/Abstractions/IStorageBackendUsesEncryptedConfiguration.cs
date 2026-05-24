// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Storage.Abstractions
{
    /// <summary>
    /// Marker for storage backends that require decrypted runtime configuration before use.
    /// </summary>
    public interface IStorageBackendUsesEncryptedConfiguration
    {
    }
}
