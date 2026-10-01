// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Integrity;
using Cotton.Server.Services.DatabaseIntegrity;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Cotton.Server.Services.Startup
{
    internal class DatabaseIntegrityStartupCheck(
        CottonDbContext dbContext,
        IDatabaseIntegrityDescriptorRegistry descriptors) : IStartupCheck
    {
        public Task<StartupBlocker?> ValidateAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            HashSet<Type> describedTypes = descriptors.All.Select(descriptor => descriptor.EntityType).ToHashSet();
            HashSet<Type> configuredTypes = [];
            foreach (IEntityType entityType in dbContext.Model.GetEntityTypes())
            {
                IProperty? version = entityType.FindProperty(DatabaseIntegrityColumns.VersionProperty);
                IProperty? mac = entityType.FindProperty(DatabaseIntegrityColumns.MacProperty);
                if (version is null && mac is null)
                {
                    continue;
                }

                if (version is null || mac is null || !mac.IsConcurrencyToken)
                {
                    throw new InvalidOperationException(
                        $"Database integrity columns are incomplete for {entityType.ClrType.Name}.");
                }

                configuredTypes.Add(entityType.ClrType);
            }

            if (!configuredTypes.SetEquals(describedTypes))
            {
                string missingDescriptors = string.Join(", ", configuredTypes.Except(describedTypes)
                    .Select(type => type.Name).Order(StringComparer.Ordinal));
                string missingColumns = string.Join(", ", describedTypes.Except(configuredTypes)
                    .Select(type => type.Name).Order(StringComparer.Ordinal));
                throw new InvalidOperationException(
                    $"Database integrity configuration differs from descriptors. "
                    + $"Missing descriptors: [{missingDescriptors}]. Missing columns: [{missingColumns}].");
            }

            return Task.FromResult<StartupBlocker?>(null);
        }
    }
}
