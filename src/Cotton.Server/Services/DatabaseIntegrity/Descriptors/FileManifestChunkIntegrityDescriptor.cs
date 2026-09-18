// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Services.DatabaseIntegrity.Descriptors
{
    public class FileManifestChunkIntegrityDescriptor : DatabaseIntegrityDescriptor<FileManifestChunk>
    {
        public const int LatestVersion = 1;

        public override string EntityName => "file_manifest_chunks";

        public override int SchemaVersion => LatestVersion;

        public override string GetEntityKey(FileManifestChunk entity)
        {
            return entity.Id.ToString("D");
        }

        public override void WriteCanonicalData(DatabaseIntegrityCanonicalWriter writer, FileManifestChunk entity, int version)
        {
            writer.WriteGuidField(nameof(entity.Id), entity.Id);
            writer.WriteGuidField(nameof(entity.FileManifestId), entity.FileManifestId);
            writer.WriteInt32Field(nameof(entity.ChunkOrder), entity.ChunkOrder);
            writer.WriteBytesField(nameof(entity.ChunkHash), entity.ChunkHash);
        }
    }
}
