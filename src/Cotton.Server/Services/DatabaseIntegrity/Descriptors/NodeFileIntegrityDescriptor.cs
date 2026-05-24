// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Services.DatabaseIntegrity.Descriptors;

/// <summary>
/// Describes the file-entry row that binds a user-visible name and node location to immutable file content.
/// </summary>
public sealed class NodeFileIntegrityDescriptor : DatabaseIntegrityDescriptor<NodeFile>
{
    /// <inheritdoc />
    public override string EntityName => "node_files";
    /// <inheritdoc />
    public override int SchemaVersion => 1;

    /// <inheritdoc />
    public override string GetEntityKey(NodeFile entity)
    {
        return entity.Id.ToString("D");
    }

    /// <inheritdoc />
    public override void WriteCanonicalData(DatabaseIntegrityCanonicalWriter writer, NodeFile entity)
    {
        writer.WriteGuidField(nameof(entity.Id), entity.Id);
        writer.WriteGuidField(nameof(entity.OwnerId), entity.OwnerId);
        writer.WriteGuidField(nameof(entity.FileManifestId), entity.FileManifestId);
        writer.WriteGuidField(nameof(entity.NodeId), entity.NodeId);
        writer.WriteGuidField(nameof(entity.OriginalNodeFileId), entity.OriginalNodeFileId);
        writer.WriteStringField(nameof(entity.Name), entity.Name);
        writer.WriteStringField(nameof(entity.NameKey), entity.NameKey);
        writer.WriteStringDictionaryField(nameof(entity.Metadata), entity.Metadata);
    }
}
