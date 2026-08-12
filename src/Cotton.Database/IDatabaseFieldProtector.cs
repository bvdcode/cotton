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
        string Protect(string plaintext);

        string Unprotect(string protectedValue);
    }
}
