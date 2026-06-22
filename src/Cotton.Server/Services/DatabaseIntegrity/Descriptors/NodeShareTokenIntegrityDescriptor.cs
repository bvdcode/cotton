// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Services.DatabaseIntegrity.Descriptors
{
    /// <summary>
    /// Describes public share-token rows protected by database integrity signing.
    /// </summary>
    /// <remarks>
    /// The MAC prevents a database-only edit from retargeting a public link, extending its lifetime, or changing the user who
    /// created the share.
    /// </remarks>
    public sealed class NodeShareTokenIntegrityDescriptor : DatabaseIntegrityDescriptor<NodeShareToken>
    {
        /// <inheritdoc />
        public override string EntityName => "node_share_tokens";
        /// <inheritdoc />
        public override int SchemaVersion => 1;

        /// <inheritdoc />
        public override string GetEntityKey(NodeShareToken entity)
        {
            return entity.Id.ToString("D");
        }

        /// <inheritdoc />
        public override void WriteCanonicalData(DatabaseIntegrityCanonicalWriter writer, NodeShareToken entity)
        {
            writer.WriteGuidField(nameof(entity.Id), entity.Id);
            writer.WriteStringField(nameof(entity.Token), entity.Token);
            writer.WriteGuidField(nameof(entity.NodeId), entity.NodeId);
            writer.WriteGuidField(nameof(entity.CreatedByUserId), entity.CreatedByUserId);
            writer.WriteNullableDateTimeField(nameof(entity.ExpiresAt), entity.ExpiresAt);
        }
    }
}
