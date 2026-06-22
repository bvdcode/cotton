// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Services.DatabaseIntegrity.Descriptors;

/// <summary>
/// Describes one ordered manifest-to-chunk mapping row.
/// </summary>
public sealed class FileManifestChunkIntegrityDescriptor : DatabaseIntegrityDescriptor<FileManifestChunk>
{
    /// <inheritdoc />
    public override string EntityName => "file_manifest_chunks";
    /// <inheritdoc />
    public override int SchemaVersion => 1;

    /// <inheritdoc />
    public override string GetEntityKey(FileManifestChunk entity)
    {
        return entity.Id.ToString("D");
    }

    /// <inheritdoc />
    public override void WriteCanonicalData(DatabaseIntegrityCanonicalWriter writer, FileManifestChunk entity)
    {
        writer.WriteGuidField(nameof(entity.Id), entity.Id);
        writer.WriteGuidField(nameof(entity.FileManifestId), entity.FileManifestId);
        writer.WriteInt32Field(nameof(entity.ChunkOrder), entity.ChunkOrder);
        writer.WriteBytesField(nameof(entity.ChunkHash), entity.ChunkHash);
    }
}
