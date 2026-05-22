// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Services.DatabaseIntegrity.Descriptors;

public sealed class DownloadTokenIntegrityDescriptor : DatabaseIntegrityDescriptor<DownloadToken>
{
    public override string EntityName => "download_tokens";
    public override int SchemaVersion => 1;

    public override string GetEntityKey(DownloadToken entity)
    {
        return entity.Id.ToString("D");
    }

    public override void WriteCanonicalData(DatabaseIntegrityCanonicalWriter writer, DownloadToken entity)
    {
        writer.WriteGuidField(nameof(entity.Id), entity.Id);
        writer.WriteStringField(nameof(entity.Token), entity.Token);
        writer.WriteGuidField(nameof(entity.NodeFileId), entity.NodeFileId);
        writer.WriteGuidField(nameof(entity.CreatedByUserId), entity.CreatedByUserId);
        writer.WriteNullableDateTimeField(nameof(entity.ExpiresAt), entity.ExpiresAt);
        writer.WriteBooleanField(nameof(entity.DeleteAfterUse), entity.DeleteAfterUse);
    }
}
