// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Services.DatabaseIntegrity.Descriptors
{
    public class FileManifestIntegrityDescriptor : DatabaseIntegrityDescriptor<FileManifest>
    {
        public const int LatestVersion = 2;

        public override string EntityName => "file_manifests";

        public override int SchemaVersion => LatestVersion;

        public override IReadOnlyCollection<int> SupportedVersions => [1, 2];

        public override string GetEntityKey(FileManifest entity)
        {
            return entity.Id.ToString("D");
        }

        public override void WriteCanonicalData(DatabaseIntegrityCanonicalWriter writer, FileManifest entity, int version)
        {
            switch (version)
            {
                case 1:
                    WriteVersion1(writer, entity);
                    break;
                case 2:
                    WriteVersion2(writer, entity);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(version), version, "Unsupported file manifest signature version.");
            }
        }

        [Obsolete("Signature version 1 requires FileManifest.ContentType. Remove together after all manifests have version 2 signatures.")]
        private static void WriteVersion1(DatabaseIntegrityCanonicalWriter writer, FileManifest entity)
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

        private static void WriteVersion2(DatabaseIntegrityCanonicalWriter writer, FileManifest entity)
        {
            writer.WriteGuidField(nameof(entity.Id), entity.Id);
            writer.WriteBytesField(nameof(entity.ComputedContentHash), entity.ComputedContentHash);
            writer.WriteBytesField(nameof(entity.ProposedContentHash), entity.ProposedContentHash);
            writer.WriteInt64Field(nameof(entity.SizeBytes), entity.SizeBytes);
            writer.WriteBytesField(nameof(entity.SmallFilePreviewHashEncrypted), entity.SmallFilePreviewHashEncrypted);
            writer.WriteBytesField(nameof(entity.SmallFilePreviewHash), entity.SmallFilePreviewHash);
            writer.WriteBytesField(nameof(entity.LargeFilePreviewHash), entity.LargeFilePreviewHash);
        }
    }
}
