// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity;

/// <summary>
/// Immutable lookup table for database-integrity descriptors.
/// </summary>
public sealed class DatabaseIntegrityDescriptorRegistry : IDatabaseIntegrityDescriptorRegistry
{
    private readonly IReadOnlyDictionary<Type, IDatabaseIntegrityDescriptor> _descriptors;

    /// <summary>Initializes the registry from dependency-injected descriptors.</summary>
    public DatabaseIntegrityDescriptorRegistry(IEnumerable<IDatabaseIntegrityDescriptor> descriptors)
    {
        _descriptors = descriptors.ToDictionary(x => x.EntityType);
        All = _descriptors.Values
            .OrderBy(x => x.EntityName, StringComparer.Ordinal)
            .ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyCollection<IDatabaseIntegrityDescriptor> All { get; }

    /// <inheritdoc />
    public bool TryGet(Type entityType, out IDatabaseIntegrityDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        if (_descriptors.TryGetValue(entityType, out descriptor!))
        {
            return true;
        }

        // EF can hand us derived proxy/runtime types. Resolve them back to the descriptor for the mapped base entity
        // instead of forcing every caller to normalize CLR types first.
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
