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
        /// Tries to resolve the descriptor schema that matches stored row metadata.
        /// </summary>
        public static bool TryResolve(
            IDatabaseIntegrityDescriptor currentDescriptor,
            int schemaVersion,
            out IDatabaseIntegrityDescriptor resolvedDescriptor)
        {
            ArgumentNullException.ThrowIfNull(currentDescriptor);

            if (currentDescriptor.SchemaVersion == schemaVersion)
            {
                resolvedDescriptor = currentDescriptor;
                return true;
            }

            if (currentDescriptor is IDatabaseIntegrityDescriptorVersionSet versionSet)
            {
                foreach (IDatabaseIntegrityDescriptor legacyDescriptor in versionSet.LegacyDescriptors)
                {
                    ValidateLegacyDescriptor(currentDescriptor, legacyDescriptor);
                    if (legacyDescriptor.SchemaVersion == schemaVersion)
                    {
                        resolvedDescriptor = legacyDescriptor;
                        return true;
                    }
                }
            }

            resolvedDescriptor = null!;
            return false;
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
