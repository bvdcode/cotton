// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity
{
    public interface IDatabaseIntegrityDescriptor<in T> : IDatabaseIntegrityDescriptor
        where T : class
    {
        string GetEntityKey(T entity);

        /// <summary>
        /// Writes the security-sensitive domain fields for the entity in deterministic order.
        /// </summary>
        void WriteCanonicalData(DatabaseIntegrityCanonicalWriter writer, T entity);
    }
}
