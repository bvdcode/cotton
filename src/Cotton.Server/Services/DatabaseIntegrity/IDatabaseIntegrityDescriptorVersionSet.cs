// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity
{
    /// <summary>
    /// Exposes older descriptor schemas that can still verify rows signed before the current schema version.
    /// </summary>
    public interface IDatabaseIntegrityDescriptorVersionSet
    {
        /// <summary>
        /// Gets previous descriptor schemas for the same entity type.
        /// </summary>
        IReadOnlyCollection<IDatabaseIntegrityDescriptor> LegacyDescriptors { get; }
    }
}
