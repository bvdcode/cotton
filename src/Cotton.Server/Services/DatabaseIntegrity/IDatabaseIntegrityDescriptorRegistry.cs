// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity;

/// <summary>
/// Resolves database-integrity descriptors by EF entity type.
/// </summary>
public interface IDatabaseIntegrityDescriptorRegistry
{
    /// <summary>Gets all registered descriptors in deterministic order for diagnostics and bridge backfill.</summary>
    IReadOnlyCollection<IDatabaseIntegrityDescriptor> All { get; }

    /// <summary>Attempts to find the descriptor that protects the supplied EF entity type.</summary>
    bool TryGet(Type entityType, out IDatabaseIntegrityDescriptor descriptor);
}
