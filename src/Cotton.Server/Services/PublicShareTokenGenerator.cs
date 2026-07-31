// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Security.Cryptography;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Generates compact tokens for public file and folder share links.
    /// </summary>
    public static class PublicShareTokenGenerator
    {
        /// <summary>
        /// Length of an automatically generated public share token.
        /// </summary>
        public const int TokenLength = 8;

        private const string Alphabet = "abcdefghijklmnopqrstuvwxyz";

        /// <summary>
        /// Creates a cryptographically random lowercase token.
        /// </summary>
        public static string Create() => RandomNumberGenerator.GetString(Alphabet, TokenLength);
    }
}
