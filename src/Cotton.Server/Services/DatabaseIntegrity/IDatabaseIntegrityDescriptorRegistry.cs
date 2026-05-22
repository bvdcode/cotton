// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity;

/// <summary>
/// Registry of all entity descriptors protected by database integrity signing.
/// </summary>
public interface IDatabaseIntegrityDescriptorRegistry
{
    /// <summary>Gets every registered descriptor.</summary>
    IReadOnlyCollection<IDatabaseIntegrityDescriptor> All { get; }
    /// <summary>Finds the descriptor for an EF entity CLR type.</summary>
    bool TryGet(Type entityType, out IDatabaseIntegrityDescriptor descriptor);
}
