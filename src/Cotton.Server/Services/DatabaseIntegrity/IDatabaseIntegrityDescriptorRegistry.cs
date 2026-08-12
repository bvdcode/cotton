// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity
{
    public interface IDatabaseIntegrityDescriptorRegistry
    {
        /// <summary>
        /// Gets all registered descriptors in deterministic order for diagnostics.
        /// </summary>
        IReadOnlyCollection<IDatabaseIntegrityDescriptor> All { get; }

        bool TryGet(Type entityType, out IDatabaseIntegrityDescriptor descriptor);
    }
}
