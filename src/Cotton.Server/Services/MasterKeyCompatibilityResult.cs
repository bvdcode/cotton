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
    /// Represents the result of master key compatibility.
    /// </summary>
    public record MasterKeyCompatibilityResult(
        bool Success,
        bool ExistingDataFound,
        bool EvidenceFound,
        string? Error)
    {
        /// <summary>
        /// Creates a successful master-key compatibility result.
        /// </summary>
        public static MasterKeyCompatibilityResult Compatible(bool existingDataFound, bool evidenceFound) =>
            new(true, existingDataFound, evidenceFound, null);

        /// <summary>
        /// Creates a failed compatibility probe result.
        /// </summary>
        public static MasterKeyCompatibilityResult Fail(
            string error,
            bool existingDataFound = true,
            bool evidenceFound = false) =>
            new(false, existingDataFound, evidenceFound, error);
    }
}
