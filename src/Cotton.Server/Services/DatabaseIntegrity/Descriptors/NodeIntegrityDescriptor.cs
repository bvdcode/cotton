// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Services.DatabaseIntegrity.Descriptors
{
    /// <summary>
    /// Describes node fields that determine where a file entry is visible and which logical tree owns it.
    /// </summary>
    public class NodeIntegrityDescriptor : DatabaseIntegrityDescriptor<Node>
    {
        /// <inheritdoc />
        public override string EntityName => "nodes";

        /// <inheritdoc />
        public override int SchemaVersion => 1;

        /// <inheritdoc />
        public override string GetEntityKey(Node entity)
        {
            return entity.Id.ToString("D");
        }

        /// <inheritdoc />
        public override void WriteCanonicalData(DatabaseIntegrityCanonicalWriter writer, Node entity)
        {
            writer.WriteGuidField(nameof(entity.Id), entity.Id);
            writer.WriteGuidField(nameof(entity.OwnerId), entity.OwnerId);
            writer.WriteGuidField(nameof(entity.LayoutId), entity.LayoutId);
            writer.WriteNullableGuidField(nameof(entity.ParentId), entity.ParentId);
            writer.WriteInt32Field(nameof(entity.Type), (int)entity.Type);
            writer.WriteStringField(nameof(entity.Name), entity.Name);
            writer.WriteStringField(nameof(entity.NameKey), entity.NameKey);
            writer.WriteStringDictionaryField(nameof(entity.Metadata), entity.Metadata);
        }
    }
}
