// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;
using Cotton.Crypto;
using EasyExtensions.Extensions;
using Npgsql;
using System.Security.Cryptography;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Defines the master key compatibility probe contract used by the server runtime.
    /// </summary>
    public interface IMasterKeyCompatibilityProbe
    {
        /// <summary>
        /// Validates that the supplied master key is compatible with any existing encrypted Cotton data.
        /// </summary>
        Task<MasterKeyCompatibilityResult> ValidateAsync(
            CottonEncryptionSettings encryptionSettings,
            MasterKeyCompatibilityMode mode,
            CancellationToken cancellationToken = default);
    }
}
