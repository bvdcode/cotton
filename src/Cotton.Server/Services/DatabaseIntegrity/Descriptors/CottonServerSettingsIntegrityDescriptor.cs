// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity.Descriptors
{
    /// <summary>
    /// Describes server-wide settings that affect security posture and must not be silently edited in the database.
    /// </summary>
    /// <remarks>
    /// These settings decide where encrypted chunks live, which external identity/storage providers are trusted, and what
    /// defaults new users receive. A database-only attacker changing them should trip the security check-up immediately.
    /// </remarks>
    public class CottonServerSettingsIntegrityDescriptor :
        CottonServerSettingsIntegrityDescriptorBase,
        IDatabaseIntegrityDescriptorVersionSet
    {
        private static readonly IReadOnlyCollection<IDatabaseIntegrityDescriptor> LegacyDescriptorVersions =
            [new CottonServerSettingsLegacyIntegrityDescriptor()];

        /// <inheritdoc />
        public override int SchemaVersion => 3;

        /// <inheritdoc />
        protected override bool IncludeFirebaseCloudMessagingFields => true;

        /// <inheritdoc />
        public IReadOnlyCollection<IDatabaseIntegrityDescriptor> LegacyDescriptors => LegacyDescriptorVersions;
    }
}
