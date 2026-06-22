// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity.Descriptors
{
    /// <summary>
    /// Describes server settings rows signed before Firebase Cloud Messaging settings became part of the payload.
    /// </summary>
    public class CottonServerSettingsLegacyIntegrityDescriptor : CottonServerSettingsIntegrityDescriptorBase
    {
        /// <inheritdoc />
        public override int SchemaVersion => 2;

        /// <inheritdoc />
        protected override bool IncludeFirebaseCloudMessagingFields => false;
    }
}
