// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Services.DatabaseIntegrity.Descriptors
{
    /// <summary>
    /// Describes one-time download-token rows protected by database integrity signing.
    /// </summary>
    /// <remarks>
    /// The MAC binds the token to its target file, creator, expiry, and consume-on-use behavior. Display filename metadata is
    /// excluded because changing it cannot grant access to a different file.
    /// </remarks>
    public class DownloadTokenIntegrityDescriptor : DatabaseIntegrityDescriptor<DownloadToken>
    {
        /// <inheritdoc />
        public override string EntityName => "download_tokens";
        /// <inheritdoc />
        public override int SchemaVersion => 1;

        /// <inheritdoc />
        public override string GetEntityKey(DownloadToken entity)
        {
            return entity.Id.ToString("D");
        }

        /// <inheritdoc />
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
}
