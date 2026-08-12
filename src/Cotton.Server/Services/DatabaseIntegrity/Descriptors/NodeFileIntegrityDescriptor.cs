// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Services.DatabaseIntegrity.Descriptors
{
    public class NodeFileIntegrityDescriptor : DatabaseIntegrityDescriptor<NodeFile>
    {
        public override string EntityName => "node_files";

        public override int SchemaVersion => 1;

        public override string GetEntityKey(NodeFile entity)
        {
            return entity.Id.ToString("D");
        }

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
}
