// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity;

public sealed class DatabaseIntegrityDescriptorRegistry : IDatabaseIntegrityDescriptorRegistry
{
    private readonly IReadOnlyDictionary<Type, IDatabaseIntegrityDescriptor> _descriptors;

    public DatabaseIntegrityDescriptorRegistry(IEnumerable<IDatabaseIntegrityDescriptor> descriptors)
    {
        _descriptors = descriptors.ToDictionary(x => x.EntityType);
        All = _descriptors.Values
            .OrderBy(x => x.EntityName, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyCollection<IDatabaseIntegrityDescriptor> All { get; }

    public bool TryGet(Type entityType, out IDatabaseIntegrityDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        if (_descriptors.TryGetValue(entityType, out descriptor!))
        {
            return true;
        }

        foreach ((Type descriptorType, IDatabaseIntegrityDescriptor candidate) in _descriptors)
        {
            if (descriptorType.IsAssignableFrom(entityType))
            {
                descriptor = candidate;
                return true;
            }
        }

        descriptor = null!;
        return false;
    }
}
