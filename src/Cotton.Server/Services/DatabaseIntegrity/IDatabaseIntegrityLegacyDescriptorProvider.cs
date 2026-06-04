// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity;

/// <summary>
/// Provides older canonical payload descriptors that are safe to upgrade to the current descriptor.
/// </summary>
public interface IDatabaseIntegrityLegacyDescriptorProvider
{
    /// <summary>Gets legacy descriptors that may verify an existing row before it is re-signed.</summary>
    IReadOnlyCollection<IDatabaseIntegrityDescriptor> LegacyDescriptors { get; }
}
