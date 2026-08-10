// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Database
{
    /// <summary>
    /// Protects sensitive string values persisted in database columns.
    /// </summary>
    /// <remarks>
    /// Implementations used by <see cref="CottonDbContext"/> must be thread-safe and live for the
    /// lifetime of the application because Entity Framework caches value converters in its model.
    /// </remarks>
    public interface IDatabaseFieldProtector
    {
        /// <summary>
        /// Encrypts a plaintext database field value.
        /// </summary>
        /// <param name="plaintext">Plaintext value.</param>
        /// <returns>The protected representation.</returns>
        string Protect(string plaintext);

        /// <summary>
        /// Decrypts a protected database field value.
        /// </summary>
        /// <param name="protectedValue">Protected value.</param>
        /// <returns>The plaintext representation.</returns>
        string Unprotect(string protectedValue);
    }
}
