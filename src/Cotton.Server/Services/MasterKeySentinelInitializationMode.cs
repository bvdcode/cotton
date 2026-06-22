// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;
using Cotton.Crypto;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Lists the supported master key sentinel initialization mode values.
    /// </summary>
    public enum MasterKeySentinelInitializationMode
    {
        /// <summary>
        /// Represents the trust provided key when no probe option.
        /// </summary>
        TrustProvidedKeyWhenNoProbe,
        /// <summary>
        /// Represents the require compatibility evidence for existing data option.
        /// </summary>
        RequireCompatibilityEvidenceForExistingData
    }
}
