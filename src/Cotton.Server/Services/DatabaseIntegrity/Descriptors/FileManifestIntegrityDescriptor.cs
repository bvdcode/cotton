// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Services.DatabaseIntegrity.Descriptors
{
    public class FileManifestIntegrityDescriptor : DatabaseIntegrityDescriptor<FileManifest>
    {
        public override string EntityName => "file_manifests";

        public override int SchemaVersion => 1;

        public override string GetEntityKey(FileManifest entity)
        {
            return entity.Id.ToString("D");
        }

        public override void WriteCanonicalData(DatabaseIntegrityCanonicalWriter writer, FileManifest entity)
        {
            WriteContentIdentityFields(writer, entity);
            // Preview fields and extracted Metadata are derived cache data, not file-content identity.
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
