// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Services.DatabaseIntegrity.Descriptors
{
    /// <summary>
    /// Describes server settings rows signed after compression settings and before Firebase Cloud Messaging settings.
    /// </summary>
    public class CottonServerSettingsV2IntegrityDescriptor : CottonServerSettingsIntegrityDescriptorBase
    {
        /// <inheritdoc />
        public override int SchemaVersion => 2;

        /// <inheritdoc />
        protected override bool IncludeCompressionLevel => true;

        /// <inheritdoc />
        protected override bool IncludeFirebaseCloudMessagingFields => false;

        /// <inheritdoc />
        protected override bool IsEntityStateAllowed(CottonServerSettings settings)
        {
            return string.IsNullOrWhiteSpace(settings.FcmProjectId)
                && string.IsNullOrWhiteSpace(settings.FcmServiceAccountJsonEncrypted);
        }
    }
}
