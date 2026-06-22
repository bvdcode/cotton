// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity
{
    /// <summary>
    /// Restricts which entity states may be accepted by a descriptor schema.
    /// </summary>
    public interface IDatabaseIntegrityDescriptorStatePolicy
    {
        /// <summary>
        /// Checks whether the entity state is compatible with this descriptor schema.
        /// </summary>
        bool IsEntityStateAllowed(object entity);
    }
}
