// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity;

public interface IDatabaseIntegrityDescriptorRegistry
{
    IReadOnlyCollection<IDatabaseIntegrityDescriptor> All { get; }
    bool TryGet(Type entityType, out IDatabaseIntegrityDescriptor descriptor);
}
