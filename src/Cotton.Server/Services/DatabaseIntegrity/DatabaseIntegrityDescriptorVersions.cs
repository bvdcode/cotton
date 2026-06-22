// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity
{
    /// <summary>
    /// Resolves descriptor versions accepted for database-integrity verification.
    /// </summary>
    public static class DatabaseIntegrityDescriptorVersions
    {
        /// <summary>
        /// Tries to verify an entity with the descriptor schemas accepted for the stored row metadata.
        /// </summary>
        public static bool TryVerify(
            object entity,
            IDatabaseIntegrityDescriptor currentDescriptor,
            int schemaVersion,
            byte[] mac,
            IDatabaseIntegrityProtector protector,
            out IDatabaseIntegrityDescriptor resolvedDescriptor)
        {
            ArgumentNullException.ThrowIfNull(entity);
            ArgumentNullException.ThrowIfNull(currentDescriptor);
            ArgumentNullException.ThrowIfNull(mac);
            ArgumentNullException.ThrowIfNull(protector);

            if (!TryResolve(
                currentDescriptor,
                schemaVersion,
                out resolvedDescriptor))
            {
                return false;
            }

            return IsEntityStateAllowed(resolvedDescriptor, entity)
                && protector.Verify(entity, resolvedDescriptor, mac);
        }

        /// <summary>
        /// Gets all schema versions that are accepted for metadata-only diagnostics.
        /// </summary>
        public static int[] GetAcceptedSchemaVersions(IDatabaseIntegrityDescriptor currentDescriptor)
        {
            ArgumentNullException.ThrowIfNull(currentDescriptor);

            List<int> versions = [currentDescriptor.SchemaVersion];
            if (currentDescriptor is IDatabaseIntegrityDescriptorVersionSet versionSet)
            {
                foreach (IDatabaseIntegrityDescriptor legacyDescriptor in versionSet.LegacyDescriptors)
                {
                    ValidateLegacyDescriptor(currentDescriptor, legacyDescriptor);
                    versions.Add(legacyDescriptor.SchemaVersion);
                }
            }

            return [.. versions.Distinct()];
        }

        private static bool TryResolve(
            IDatabaseIntegrityDescriptor currentDescriptor,
            int schemaVersion,
            out IDatabaseIntegrityDescriptor resolvedDescriptor)
        {
            IDatabaseIntegrityDescriptor? match = null;
            if (currentDescriptor.SchemaVersion == schemaVersion)
            {
                match = currentDescriptor;
            }

            if (currentDescriptor is IDatabaseIntegrityDescriptorVersionSet versionSet)
            {
                foreach (IDatabaseIntegrityDescriptor legacyDescriptor in versionSet.LegacyDescriptors)
                {
                    ValidateLegacyDescriptor(currentDescriptor, legacyDescriptor);
                    if (legacyDescriptor.SchemaVersion != schemaVersion)
                    {
                        continue;
                    }

                    if (match is not null)
                    {
                        throw new InvalidOperationException(
                            $"Multiple integrity descriptors are registered for {currentDescriptor.EntityName} schema version {schemaVersion}.");
                    }

                    match = legacyDescriptor;
                }
            }

            resolvedDescriptor = match!;
            return match is not null;
        }

        private static bool IsEntityStateAllowed(IDatabaseIntegrityDescriptor descriptor, object entity)
        {
            return descriptor is not IDatabaseIntegrityDescriptorStatePolicy statePolicy
                || statePolicy.IsEntityStateAllowed(entity);
        }

        private static void ValidateLegacyDescriptor(
            IDatabaseIntegrityDescriptor currentDescriptor,
            IDatabaseIntegrityDescriptor legacyDescriptor)
        {
            if (legacyDescriptor.EntityType != currentDescriptor.EntityType)
            {
                throw new InvalidOperationException(
                    $"Legacy descriptor for {legacyDescriptor.EntityName} does not match current entity type.");
            }

            if (legacyDescriptor.EntityName != currentDescriptor.EntityName)
            {
                throw new InvalidOperationException(
                    $"Legacy descriptor for {legacyDescriptor.EntityName} does not match current entity name.");
            }
        }
    }
}
