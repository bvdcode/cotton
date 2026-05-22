// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Services.DatabaseIntegrity.Descriptors;

public sealed class NodeShareTokenIntegrityDescriptor : DatabaseIntegrityDescriptor<NodeShareToken>
{
    public override string EntityName => "node_share_tokens";
    public override int SchemaVersion => 1;

    public override string GetEntityKey(NodeShareToken entity)
    {
        return entity.Id.ToString("D");
    }

    public override void WriteCanonicalData(DatabaseIntegrityCanonicalWriter writer, NodeShareToken entity)
    {
        writer.WriteGuidField(nameof(entity.Id), entity.Id);
        writer.WriteStringField(nameof(entity.Token), entity.Token);
        writer.WriteGuidField(nameof(entity.NodeId), entity.NodeId);
        writer.WriteGuidField(nameof(entity.CreatedByUserId), entity.CreatedByUserId);
        writer.WriteNullableDateTimeField(nameof(entity.ExpiresAt), entity.ExpiresAt);
    }
}
