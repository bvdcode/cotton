// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Services.DatabaseIntegrity.Descriptors
{
    /// <summary>
    /// Describes immutable file-content metadata that must match the chunks served to a reader.
    /// </summary>
    public sealed class FileManifestIntegrityDescriptor : DatabaseIntegrityDescriptor<FileManifest>
    {
        /// <inheritdoc />
        public override string EntityName => "file_manifests";
        /// <inheritdoc />
        public override int SchemaVersion => 1;

        /// <inheritdoc />
        public override string GetEntityKey(FileManifest entity)
        {
            return entity.Id.ToString("D");
        }

        /// <inheritdoc />
        public override void WriteCanonicalData(DatabaseIntegrityCanonicalWriter writer, FileManifest entity)
        {
            WriteContentIdentityFields(writer, entity);
            // PreviewGenerationError and PreviewGeneratorVersion are retry/scheduling state, not file-content identity.
            // Keep them outside the MAC so operators can safely clear preview failures and generator bumps can reschedule work.
        }

        private static void WriteContentIdentityFields(DatabaseIntegrityCanonicalWriter writer, FileManifest entity)
        {
            writer.WriteGuidField(nameof(entity.Id), entity.Id);
            writer.WriteBytesField(nameof(entity.ComputedContentHash), entity.ComputedContentHash);
            writer.WriteBytesField(nameof(entity.ProposedContentHash), entity.ProposedContentHash);
            writer.WriteStringField(nameof(entity.ContentType), entity.ContentType);
            writer.WriteInt64Field(nameof(entity.SizeBytes), entity.SizeBytes);
            writer.WriteBytesField(nameof(entity.SmallFilePreviewHashEncrypted), entity.SmallFilePreviewHashEncrypted);
            writer.WriteBytesField(nameof(entity.SmallFilePreviewHash), entity.SmallFilePreviewHash);
            writer.WriteBytesField(nameof(entity.LargeFilePreviewHash), entity.LargeFilePreviewHash);
        }
    }
}
