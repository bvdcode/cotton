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
    /// Lists the supported master key compatibility mode values.
    /// </summary>
    public enum MasterKeyCompatibilityMode
    {
        /// <summary>
        /// Represents the allow missing evidence option.
        /// </summary>
        AllowMissingEvidence,
        /// <summary>
        /// Represents the require evidence for existing data option.
        /// </summary>
        RequireEvidenceForExistingData
    }
}
