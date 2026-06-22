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

            foreach (IDatabaseIntegrityDescriptor descriptor in EnumerateCandidateDescriptors(
                currentDescriptor,
                schemaVersion))
            {
                if (protector.Verify(entity, descriptor, mac))
                {
                    resolvedDescriptor = descriptor;
                    return true;
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

        private static IEnumerable<IDatabaseIntegrityDescriptor> EnumerateCandidateDescriptors(
            IDatabaseIntegrityDescriptor currentDescriptor,
            int schemaVersion)
        {
            if (currentDescriptor.SchemaVersion == schemaVersion)
            {
                yield return currentDescriptor;
            }

            if (currentDescriptor is not IDatabaseIntegrityDescriptorVersionSet versionSet)
            {
                yield break;
            }

            foreach (IDatabaseIntegrityDescriptor legacyDescriptor in versionSet.LegacyDescriptors)
            {
                ValidateLegacyDescriptor(currentDescriptor, legacyDescriptor);
                if (legacyDescriptor.SchemaVersion == schemaVersion)
                {
                    yield return legacyDescriptor;
                }
            }
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
