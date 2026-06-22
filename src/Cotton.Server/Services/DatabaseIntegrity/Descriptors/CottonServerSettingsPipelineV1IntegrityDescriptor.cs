// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity.Descriptors
{
    /// <summary>
    /// Describes server settings rows signed before compression level joined the canonical payload.
    /// </summary>
    public class CottonServerSettingsPipelineV1IntegrityDescriptor : CottonServerSettingsIntegrityDescriptorBase
    {
        /// <inheritdoc />
        public override int SchemaVersion => 1;

        /// <inheritdoc />
        protected override bool IncludeCompressionLevel => false;

        /// <inheritdoc />
        protected override bool IncludeFirebaseCloudMessagingFields => false;
    }
}
